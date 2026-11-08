# Politics research (seventh direction, 7 Nov 2026)

Facts for the politics view (`Docs/hills-design/POLITICS.md`; the user's direction is `Docs/research/advantage/brief-v5.md`),
each through the truth gate (its quote verbatim on the cited page, its values in the quote, Jev judging that the page supports
the claim at 0.6 or more):

    python3 -I Tools/advantage/verify.py --dir Docs/research/politics --cluster <cluster>

| Cluster | What it holds |
|---|---|
| `us-control.json` | presidents' parties and terms; House and Senate seats by party at the start of every Congress, 79th-119th (Senate Historical Office, House Historian); governors in benchmark years |
| `us-votes.json` | the presidential and national House popular vote by party, 1948-2024; turnout of the voting-age (and from 1980 eligible) population; population benchmarks |
| `us-officials.json` | vice presidents, Speakers, Senate majority leaders, Secretaries of State, Treasury and Defense, Chairmen of the Joint Chiefs, 1945-2026, with dates and parties |
| `military-power.json` | SIPRI shares of world military spending 1950-2025 (US, USSR/Russia, China, NATO, the Warsaw Pact); warhead stockpiles every year (US and USSR/Russia from 1945, China from 1964); US Navy ships FY1948-FY2025, carriers 1945-2016; China's navy 2004-2023; NATO's founding and accessions; sea control and freedom of navigation |
| `trade-balances.json` | the US balance in goods and services 1960-2025; US goods balances with China, Mexico, Canada, Japan, Germany and the EU in benchmark years; China's surplus; the sea's share of trade by volume |
| `key-moments.json` | 61 dated laws, regulations, trade actions, monetary turns, treaties and conflicts, 1944-2026 |

A skeptic then checked a sample of 829 facts for measure, date and scope; its verdict is each checked fact's `check` (24
rejected and kept with the reason: never use a fact whose `check.ok` is false). Read each cluster's gaps in its researcher's
notes before relying on a series: SIPRI's Cold War shares mix price bases (the 1950-55 world totals), the House Historian's rows
for the 85th, 87th, 96th and 99th Congresses disagree with other sources, and turnout for 1964, 1980 (VEP) and 2004 is off the
series' basis.
