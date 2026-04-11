#!/bin/bash
# This is an alternative version of afk.sh that provides the context in a separate file instead of the command line, which may help with very large contexts that exceed command line length limits.

set -eo pipefail

if [ -z "$1" ]; then
  echo "Usage: $0 <iterations>"
  exit 1
fi

# jq filter to extract streaming text from assistant messages
stream_text='select(.type == "assistant").message.content[]? | select(.type == "text").text // empty | gsub("\n"; "\r\n") | . + "\r\n\n"'

# jq filter to extract final result
final_result='select(.type == "result").result // empty'

for ((i=1; i<=$1; i++)); do
  tmpfile=$(mktemp)
  trap "rm -f $tmpfile" EXIT

  commits=$(git log -n 5 --format="%H%n%ad%n%B---" --date=short 2>/dev/null || echo "No commits found")
  issues=$(gh issue list --state open --json number,title,body,comments)
  prompt=$(cat ralph/prompt.md)

  context_file=ralph/.context_tmp.md
  trap "rm -f $tmpfile $context_file" EXIT
  printf 'Previous commits:\n%s\n\nOpen issues:\n%s\n\n%s' "$commits" "$issues" "$prompt" > "$context_file"

  docker sandbox run claude . -- \
    --verbose \
    --print \
    --output-format stream-json \
    "$prompt Context is in ralph/.context_tmp.md - read it first." \
  | grep --line-buffered '^{' \
  | tee "$tmpfile" \
  | jq --unbuffered -rj "$stream_text"

  result=$(jq -r "$final_result" "$tmpfile")

  if [[ "$result" == *"<promise>NO MORE TASKS</promise>"* ]]; then
    echo "Ralph complete after $i iterations."
    exit 0
  fi
done