# balance-b: balance sheets, P/E and the private labs (7 Nov 2026)

Method: each company's latest reported balance sheet and trailing P/E, read from companiesmarketcap.com's per-company pages (USD, dated by report), then the private labs from Reuters/FT and press reports of Anthropic's S-1. Every number below is a fact id in balance-b.json.

| Company | Assets $bn | Cash $bn | Debt $bn | Equity $bn | P/E |
|---|---|---|---|---|---|
| TSMC | 315.45 [balanceb-tsmc-totalassets] | 126.05 [balanceb-tsmc-cash] | 30.86 [balanceb-tsmc-debt] | 221.01 [balanceb-tsmc-equity] | 35.3666 [balanceb-tsmc-pe] |
| ASML | 66.8 [balanceb-asml-totalassets] | 14.42 [balanceb-asml-cash] | 2.22 [balanceb-asml-debt] | 24.99 [balanceb-asml-equity] | 35.3766 [balanceb-asml-pe] |
| SK Hynix | 225.27 [balanceb-skhynix-totalassets] | 56.81 [balanceb-skhynix-cash] | 13.63 [balanceb-skhynix-debt] | 169.63 [balanceb-skhynix-equity] | 5.93755 [balanceb-skhynix-pe] |
| Samsung Electronics | 490.42 [balanceb-samsung-totalassets] | 122.69 [balanceb-samsung-cash] | 14.47 [balanceb-samsung-debt] | 374.08 [balanceb-samsung-equity] | 10.4899 [balanceb-samsung-pe] |
| Micron | 195.88 [balanceb-micron-totalassets] | 43.43 [balanceb-micron-cash] | 5.17 [balanceb-micron-debt] | 138.37 [balanceb-micron-equity] | 9.18141 [balanceb-micron-pe] |
| Arm | 11.19 [balanceb-arm-totalassets] | 3.88 [balanceb-arm-cash] | 0.46 [balanceb-arm-debt] | 8.63 [balanceb-arm-equity] | 178.773 [balanceb-arm-pe] |
| Dell | 127.39 [balanceb-dell-totalassets] | 11.56 [balanceb-dell-cash] | 34.46 [balanceb-dell-debt] | -1.43 [balanceb-dell-equity] | 18.8146 [balanceb-dell-pe] |
| Arista | 23.72 [balanceb-arista-totalassets] | 13.34 [balanceb-arista-cash] | 0.048 [balanceb-arista-debt] | 14.79 [balanceb-arista-equity] | 53.0654 [balanceb-arista-pe] |
| GE Vernova | 86.22 [balanceb-gev-totalassets] | 16.76 [balanceb-gev-cash] | 2.79 [balanceb-gev-debt] | 12.53 [balanceb-gev-equity] | 19.9362 [balanceb-gev-pe] |
| Constellation | 98.25 [balanceb-constellation-totalassets] | 0.69 [balanceb-constellation-cash] | 24.7 [balanceb-constellation-debt] | 32.32 [balanceb-constellation-equity] | 24.5394 [balanceb-constellation-pe] |
| Berkshire Hathaway | 1263 [balanceb-berkshire-totalassets] | 365.51 [balanceb-berkshire-cash] | 128.59 [balanceb-berkshire-debt] | 750.17 [balanceb-berkshire-equity] | 15.7084 [balanceb-berkshire-pe] |
| JPMorgan Chase | 5015 [balanceb-jpmorgan-totalassets] | 567.23 [balanceb-jpmorgan-cash] | 532.95 [balanceb-jpmorgan-debt] | 374.59 [balanceb-jpmorgan-equity] | 14.1344 [balanceb-jpmorgan-pe] |
| Eli Lilly | 148.07 [balanceb-lilly-totalassets] | 4.88 [balanceb-lilly-cash] | 49.5 [balanceb-lilly-debt] | 40.27 [balanceb-lilly-equity] | 44.3184 [balanceb-lilly-pe] |
| Walmart | 293.91 [balanceb-walmart-totalassets] | 11.52 [balanceb-walmart-cash] | 73.75 [balanceb-walmart-debt] | 104.79 [balanceb-walmart-equity] | 45.4585 [balanceb-walmart-pe] |
| Tencent | 317.85 [balanceb-tencent-totalassets] | 67.99 [balanceb-tencent-cash] | 69.45 [balanceb-tencent-debt] | 179.95 [balanceb-tencent-equity] | 14.3647 [balanceb-tencent-pe] |

## What a strategist reads

- The premium shells: Arm (P/E 178.8 [balanceb-arm-pe]) on $11.2bn of assets [balanceb-arm-totalassets], Arista 53.1 [balanceb-arista-pe], Walmart 45.5 [balanceb-walmart-pe] and Eli Lilly 44.3 [balanceb-lilly-pe] are priced for growth; SK Hynix 5.9 [balanceb-skhynix-pe], Micron 9.2 [balanceb-micron-pe] and Samsung 10.5 [balanceb-samsung-pe] are priced as cyclical memory.
- Dell stands on negative equity (-$1.43bn [balanceb-dell-equity]) with $34.5bn of debt [balanceb-dell-debt]: a leveraged assembler of other people's chips.
- Fortresses: TSMC (equity $221bn [balanceb-tsmc-equity]), Berkshire (assets $1.263 trillion [balanceb-berkshire-totalassets]) and JPMorgan ($5.015 trillion [balanceb-jpmorgan-totalassets]).
- OpenAI forecasts $278bn of negative free cash flow over 2026 to 2030 [balanceb-openai-burn-forecast-2030] against $856bn of compute and infrastructure spending [balanceb-openai-compute-spend-2030]; Q1 2026 burn was $3.7bn [balanceb-openai-q1-burn].
- Anthropic raised about $141bn and held $112.4bn of cash at June 2026 [balanceb-anthropic-raised-cash], with an H1 net loss of $80.7bn [balanceb-anthropic-net-loss] and compute obligations above $400bn [balanceb-anthropic-compute-obligations]: the stock-price-funded loss the user describes, and the one a cycle turn would test first.

## Sources

companiesmarketcap.com per-company pages; Federal Reserve H.10 (2 Nov 2026); Reuters via DealStreetAsia; Crypto Briefing (The Information, Anthropic S-1 reports).

Verified: 86 of 86.
