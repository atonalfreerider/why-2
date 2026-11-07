# Entry barriers: talent, share price (capital) and custom silicon, and the entrants (cluster `entry`)

As of 7 November 2026. Fact ids in brackets starting `entry-` are verified in this cluster; others come from earlier rounds in `model.json`.

## The user's claim

"The top tech companies own talent and have a share price that prevents new entrants." Half true, with limits that matter.

## Talent: the wall is real, but it is a price list, not a lock

- **Industry owns the frontier.** Nearly 90% of notable models in 2024 came from industry, up from 60% in 2023 [entry-ai-index-industry-share].
- **Pay is paid in stock, so the share price is the talent weapon.** OpenAI's stock pay averaged $1.5 million per employee across about 4,000 staff in 2025 [entry-openai-sbc-per-employee]. NVIDIA's stock-based pay was $6.39 billion in FY2026 and it calls it foundational to attracting talent [entry-nvidia-sbc-fy2026, entry-nvidia-sbc-statement]. Meta and Alphabet booked $21.05 billion and $23.3 billion over nine months of 2026 [weapons-meta-sbc-9m, weapons-alphabet-sbc]. Meta offered up to $300 million over four years to individual researchers [labs-meta-talent-pay].
- **Incumbents buy teams instead of companies.** Microsoft took Inflection for $650 million [weapons-acq-inflection], Google paid $2.7 billion for Character.AI and $2.4 billion for Windsurf [weapons-acq-characterai, weapons-acq-windsurf], Amazon took Adept's CEO and four co-founders under a non-exclusive licence [entry-adept-amazon], NVIDIA took Enfabrica's team [entry-enfabrica-acquihire] and Groq's leaders [weapons-acq-nvidia-groq], and Meta took 49% of Scale AI and hired its CEO [str-meta-scale-14bn]. The pattern removes a nascent competitor without a merger review.
- **Limit: talent also leaves.** Eleven OpenAI staff left to found Anthropic [lineage-anthropic-founders-from-openai]. SignalFire measured 80% two-year retention at Anthropic, 78% at DeepMind, 67% at OpenAI, 64% at Meta [entry-signalfire-retention]. Mission and momentum beat cash for part of the market. Thinking Machines runs on a small team of 140 to 169 people [entry-tml-seed-stall], so losing a few seniors would hurt.

## Share price and capital: the wall is highest at the chip and cloud layers

- **Cost of a frontier run.** Gemini Ultra consumed about $191 million of compute, GPT-4 about $78 million [entry-training-cost-gemini-gpt4]. Costs grow 2.4x a year [entry-epoch-cost-growth] and will pass a billion dollars by 2027 on that trend [entry-epoch-billion-2027]. Anthropic's CEO said in 2024 that models in training could cost up to $1 billion [entry-amodei-1bn-training].
- **Incumbent capex.** The big five plan $815 billion of 2026 capex [compute-hyperscaler-capex-2026]. The largest chip start-up raise, Cerebras's IPO, was $5.5 billion [entry-cerebras-ipo]: about 0.7% of one year's capex.
- **The share price as a weapon.** NVIDIA joined nearly 67 venture deals in 2025 [entry-nvidia-venture-deals-2025], committed up to $10 billion to Anthropic [entry-nvidia-anthropic-10bn], a stake and at least one gigawatt to Thinking Machines [entry-tml-nvidia-gigawatt], up to $100 billion to OpenAI [labs-openai-nvidia-100bn]. Its customers' fundraising becomes its sales. AMD went the other way: it paid OpenAI with a warrant for up to 160 million AMD shares to win up to 6 GW of orders [entry-openai-amd-warrant]. In both, equity buys demand.

## Custom silicon: how entrants enter the hardware layer

Entry is through a customer's money, not a start-up's: Amazon's Project Rainier for Anthropic has nearly 500,000 Trainium2 chips [entry-trainium-rainier]; Google sells TPU capacity to Anthropic [labs-anthropic-google-tpu], and Meta was reported in talks to buy billions of dollars of TPUs from 2027 [entry-meta-google-tpu-talks]; OpenAI signed 10 GW of custom accelerators with Broadcom [entry-openai-broadcom-10gw]. Pure chip start-ups are funded but small and concentrated: two customers made up 62% and 24% of Cerebras's 2025 revenue [entry-cerebras-customers].

## Entrants and their fates

| Entrant | What it raised | Fate |
| --- | --- | --- |
| Anthropic | $965B valuation, funded by Amazon, Google, NVIDIA | independent; broke in via defection and customers' capital [lineage-anthropic-founders-from-openai, labs-anthropic-valuation] |
| DeepSeek | hedge-fund funded; 2.788M H800 GPU hours for V3 [entry-deepseek-v3-gpu-hours]; $52B valuation | independent; entered with a pre-control stock of 10,000+ A100s [entry-deepseek-a100-stock]; its release cost NVIDIA $589B in a day [entry-deepseek-nvidia-record-loss] |
| xAI | 100,000 H100s in 122 days [entry-xai-colossus-122-days] | merged into SpaceX at $1.25T [labs-xai-spacex-merger-value] |
| Thinking Machines | $2B at $12B; $50B ask passed [entry-tml-seed-stall] | independent, stalled |
| Safe Superintelligence | $2B at $32B, no product [entry-ssi-32bn] | independent |
| Mistral | EUR 1.7B at EUR 11.7B; ASML 11% [entry-mistral-series-c, entry-mistral-asml-stake] | independent, supplier-backed |
| Reflection AI | $2B at $8B [entry-reflection-8bn]; first model Oct 2026 matched a prior-generation Chinese open model [entry-reflection-beam] | independent |
| Moonshot, Zhipu, MiniMax | $35B valuation [entry-moonshot-35bn]; HK IPOs of $558M and $619M [entry-zhipu-ipo, entry-minimax-ipo] | independent and public: a second capital wall in China |
| Inflection, Character.AI, Windsurf, Adept, Scale | see above | licence-and-hire or minority stake |
| Cerebras | IPO $5.5B [entry-cerebras-ipo] | public |
| Groq | $640M at $2.8B [entry-groq-series-d] | licence-and-hire by NVIDIA |
| Enfabrica | | team hired by NVIDIA |
| SambaNova | $350M [entry-sambanova-feb2026] | independent |
| Tenstorrent | $1.5B at $6.2B [entry-tenstorrent-series-e] | independent |
| Etched | $300M at $10.3B then $700M at $21B [entry-etched-series-c, entry-etched-21bn] | independent |

## What a strategist would act on

1. **Three ways in worked: defection, a patient state or fund, and a customer's balance sheet.** Anthropic (defection plus Amazon/Google/NVIDIA money), DeepSeek (a hedge fund and a pre-control GPU stock) and xAI (a billionaire's capital and speed) each had capital or compute from outside the incumbents' wall.
2. **The wall is capital at chips and clouds, talent at models.** A $1 billion-plus training run and $815 billion of incumbent capex price out all but a few; talent can leave, and most entrants listed here exist because someone with a large balance sheet paid for them.
3. **The most common fate of a promising entrant is absorption without acquisition.** Seven licence-and-hire or stake deals in two years leave the shell independent on paper.
4. **NVIDIA's share price converts into demand.** It funds the labs that buy its chips; watch NVIDIA's investment commitments as an indicator, and customer-concentration risk for any chip start-up.
5. **Fragility.** Valuations of Etched (doubled in weeks) and Thinking Machines (a $50 billion ask that stalled) show the entrant market is priced on narrative; a drop in NVIDIA's share price would shrink the funding that props up its customers.

## Second pass: sizes, casualties, the platforms' strata, custom silicon

- **Every entrant now has a size.** Valuations: Thinking Machines $12B [entry-tml-valuation], SSI $32B [entry-ssi-valuation], Reflection $8B [entry-reflection-valuation], Mistral nearly $14B [entry-mistral-valuation], Moonshot $35B [entry-moonshot-valuation], Tenstorrent $6.2B [entry-tenstorrent-valuation], Etched $21B [entry-etched-valuation], SambaNova $11B in July 2026 [entry-sambanova-valuation], Groq $6.9B before NVIDIA's deal [entry-groq-valuation-2025], Scale AI $14B in 2024 [entry-scale-valuation-2024]. Market caps on 7 Nov: Cerebras $21.3B [entry-cerebras-mcap], MiniMax $6.93B [entry-minimax-mcap], Zhipu HKD 198.45B, about $25B [entry-zhipu-mcap, entry-hkd-peg-band]. Against Microsoft, Apple, Alphabet and the rest in the trillions, the largest entrant is under 3% of an incumbent: that gap is the share-price wall.
- **Casualties.** Graphcore was valued at $2.8B in 2020 [entry-graphcore-peak-2020] and sold to SoftBank for about $500M [entry-graphcore-softbank]. Humane raised over $230M [entry-humane-raised] and sold most assets to HP for $116M while the AI Pin was shut down [entry-humane-hp-sale]. Builder.ai took $230M in 2023 [entry-builderai-raised] and went insolvent in May 2025 [entry-builderai-insolvency]. Enfabrica's CEO, staff and technology cost NVIDIA more than $900M [entry-enfabrica-acquihire].
- **Where the platforms stand.** In clouds the only listed entrant, CoreWeave, is worth $25.59B [str-mcap-coreweave]. In consumer, Perplexity is valued at $21.21B [entry-perplexity-valuation] against Search; Cursor reached $29.3B [entry-cursor-valuation] and then was absorbed by SpaceXAI from June 2026 [entry-cursor-spacex]. The consumer capital barrier is the share price as currency: WhatsApp, YouTube and io were bought largely or wholly in stock [weapons-acq-whatsapp, weapons-acq-youtube, weapons-acq-openai-io].
- **Talent at every platform.** Stock-based pay: Amazon $15.476B in nine months of 2026 [entry-amazon-sbc-9m], Microsoft $12.405B in FY2026 and $3.134B last quarter [entry-microsoft-sbc-fy2026, entry-microsoft-sbc-q1fy27], Apple $13.789B in FY2026 [entry-apple-sbc-fy2026]. Each is paid in a share price entrants cannot match. A talent-concentration figure from the AI Index was not available as a number.
- **Custom silicon, the main force on NVIDIA.** Google plans 4.3M TPUs in 2026 [str-google-tpu-half]; Amazon holds over $225B of Trainium commitments [integration-amazon-trainium-commitments] on nearly 500,000 Trainium2 in Rainier [entry-trainium-rainier]; OpenAI signed 10 GW with Broadcom [entry-openai-broadcom-10gw]; Microsoft introduced Maia 200 [entry-microsoft-maia200]; Meta keeps MTIA [entry-meta-mtia-2024] and, after the November 2025 talks [entry-meta-google-tpu-talks], was reported in February 2026 to have agreed to rent Google's TPUs, with a purchase still unagreed [entry-meta-google-tpu-rental-feb2026]. The flashpoint is now `fp-entry-custom-silicon`.
- **Capital rivers into entrants.** ASML to Mistral $1.5B [entry-asml-mistral-usd]; NVIDIA to Enfabrica $0.9B; Meta to Scale AI $14B [entry-scale-meta-stake]; SoftBank to Graphcore $0.5B; HP to Humane $0.116B. The NVIDIA to Anthropic river is booked once, by the moats cluster.
- **Corrections.** The Etched Series C and Enfabrica facts now quote their numbers; the Meta $300M talent figure rests on labs-meta-talent-pay only.

## Sources

Stanford HAI AI Index 2025; Yahoo Finance (WSJ); SEC-derived data (AlphaQuery); NVIDIA IR; GeekWire; The Decoder; SiliconANGLE; arXiv (Epoch AI; DeepSeek-V3); Tom's Hardware; Reuters via NST; Data Center Dynamics; AMD press release; tech.eu; Maginative; StartupHub; Wikipedia (Reflection, Cerebras, Groq, SambaNova, Anthropic, DeepSeek); TechCrunch; Let's Data Science; Startup Fortune; AI Weekly; The Star; OfficeChai; Yicai; SignalFire; CSIS.

Also: SEC-derived 10-K copies (Microsoft via Fortune/Quartr, Apple via its IR host); companiesmarketcap; stockanalysis; HKMA; Wikipedia (Graphcore, Scale AI, Perplexity, Cursor, SambaNova, Builder.ai); TechCrunch (Humane); Dow Jones via Tiger Brokers.

Verified: 75 of 75 facts (gate run 7 Nov 2026), plus earlier-round facts cited by id.
