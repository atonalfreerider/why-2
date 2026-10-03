# Working on why-2

This is a Unity project. Work from the repository root and preserve Unity asset
metadata. Read README.md and the relevant project code before changing behavior.
The user controls local coding work through Codex desktop or the interactive CLI.

## Jev

For structured AI decisions, read the official TypeSafe skill at
`.agents/skills/typesafe-ai/SKILL.md` and setup instructions in `Docs/jev-setup.md`.
The local caller is `Tools/jev.ps1`, which uses Vercel AI Gateway and reads
`AI_GATEWAY_API_KEY` from its process environment. Check whether the credential
is available without printing its value. If it is unavailable, report that live
Jev calls are unavailable; continue ordinary coding work where possible.

Jev can return classifications, scores, and verification probabilities. It does
not perform edits or execute tools. Use it where a semantic judgment helps the
user's task; keep exact calculations, permissions, and tool execution in code.
Send only task-relevant inputs. Do not include credentials in request files,
console output, or commits. Treat model confidence as evidence, not authorization.
