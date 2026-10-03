# Jev for why-2

The official TypeSafe agent skill is installed at
`.agents/skills/typesafe-ai/SKILL.md`. It is versioned with this repository so
agents working on a clone can use it. Ask your agent to use the TypeSafe skill.
The skill supplies API guidance; actual model calls use credentials separately.

Jev returns typed decisions and probabilities. Coding agents can call it for
classification, routing, scoring, or verification while they continue writing
and testing code. This setup does not add AI behavior to Unity scenes.

## Control and monitor from the desktop

Double-click `Open-Codex.cmd` in this folder to open this project in the Codex
desktop app. Give it a concrete task, then review the chat, progress, and code
changes there. The installed Codex CLI is already logged in using ChatGPT.

For a terminal interface, double-click `Start-Agent.cmd`. It starts an interactive
coding agent rooted in this folder. `Monitor-Agents.cmd` opens the local agent
session browser. These launchers do not start unattended tasks or alter approval
settings. Repository guidance in `AGENTS.md` points agents to Jev when useful.

The launchers require `codex` on PATH, as on this PC. The installed skill will be
available to a new project agent session.

## Vercel AI Gateway

The local Git remote is `https://github.com/atonalfreerider/why-2.git`.
The Vercel project is `https://vercel.com/meta-virtuoso/why-2`.
Jev is accessed through Vercel AI Gateway using model `typesafe-ai/jev`.
No Vercel deployment is required for local agent calls.

Create an AI Gateway key in Vercel's AI Gateway / API Keys page. Keep it local;
do not paste it into chat or commit it. In PowerShell, enter it without echoing:

```powershell
$jevSecret = Read-Host 'Vercel AI Gateway API key' -AsSecureString
$env:AI_GATEWAY_API_KEY = [System.Net.NetworkCredential]::new('', $jevSecret).Password
```

From the repository root:

```powershell
# Offline structural check; no key or model usage needed.
powershell -NoProfile -File Tools/jev.ps1 -RequestPath Tools/jev-smoke.json -ValidateOnly

# Live smoke test; subject to your Vercel Gateway usage and billing.
powershell -NoProfile -File Tools/jev.ps1 -RequestPath Tools/jev-smoke.json
```

For an agent call, create a JSON file with `state` and a `questions` map and pass
its path to `Tools/jev.ps1`. Only that supplied state and those questions are sent
to the Gateway; the command does not scan or upload the repository. It prints
the provider's JSON response, including probabilities and usage. The structural
check is not a complete replacement for provider schema validation.

The key lives only in the current process environment and its child processes.
Start the coding agent from that PowerShell session if it needs to inherit it.
Remove it afterward with `Remove-Item Env:AI_GATEWAY_API_KEY`.

## References

- Official agent skill: https://docs.typesafe.ai/agent-skill
- API format: https://docs.typesafe.ai/api
- Vercel endpoint and model: https://docs.typesafe.ai/sdk/python/usage
- Upstream skill: https://github.com/typesafe-ai/skills

For semantic code search, Jevgrep is a separate community CLI:
https://github.com/dzhng/jevgrep. It is not installed by this setup.
