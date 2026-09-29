# Hand-written streams

The version folders beside this one are real `claude -p --output-format stream-json --verbose`
output, recorded by `npm run test:claude-contract -- --record` and scrubbed. The files here
are written by hand, for shapes a live probe cannot produce on demand. Each file notes which
shape it stands for in the test that reads it.

- `legacy-task.jsonl`: an older client. It names the delegation tool `Task`, sets no
  `parent_tool_use_id` and no `message.id`, and gives subagents their own `session_id`.
- `refusal.jsonl`: the account refusing the run, in the assistant's text and in the result.
- `result-refusal.jsonl`: a refusal that appears only in the error result.
- `nested-delegation.jsonl`: a subagent that delegates in turn, and a tool block repeated
  across two events of one message.
