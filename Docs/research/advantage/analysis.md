# Competitive advantage in 2026: the strategist's analysis

Written 7 November 2026. This is the reading behind the advantage hillside. It says who holds the economy's high ground, where the money runs, which contests are being fought, how the same pattern ran through history, and what is likely to happen next.

How to read it. A number in brackets is a fact that passed the truth gate: a verbatim quote from a source read today, checked by code and by Jev, TypeSafe's System One model [compute-nvidia-q2fy27-revenue]. A number marked *judged* is not a fact. It is Jev's judgment over those facts, reviewed by the strategist. Altitude is judged on Helmer's 7 Powers and runs from 0 to 7. One strong power is enough to stand high; a second and third add height.

## 1. The 2026 frontier

### Who dominates, and why

Size and advantage are different things. Apple is the largest company in the world at $4.975 trillion [economy-mcap-apple] and NVIDIA is second at $4.506 trillion [compute-nvidia-marketcap]. But the highest summit on the hillside belongs to ASML, a $549 billion company [compute-asml-marketcap]. It is the only maker of EUV lithography machines [compute-asml-euv-100], and it says its supply for 2027 and 2028 will not meet demand [compute-asml-supply-short]. Nothing can replace it. Its altitude is 6.84 of 7 (*judged*).

The first twelve firms by altitude (*judged*) read like a map of the supply chain's narrowest places:

| Rank | Firm | Altitude | Primary power |
| --- | --- | --- | --- |
| 1 | ASML | 6.84 | cornered resource (EUV) |
| 2 | TSMC | 5.95 | process power |
| 3 | KLA | 5.85 | cornered resource (inspection) |
| 4 | NVIDIA | 5.73 | switching costs (CUDA) |
| 5 | Tencent | 5.47 | network economies (WeChat) |
| 6 | Apple | 5.41 | switching costs |
| 7 | Arm | 5.36 | network economies |
| 8 | Synopsys | 5.16 | switching costs (chip design software) |
| 9 | Alphabet | 5.08 | scale economies |
| 10 | Huawei | 4.97 | cornered resource (a protected market) |
| 11 | Microsoft | 4.94 | switching costs |
| 12 | Tokyo Electron | 4.90 | cornered resource |

Six of the twelve are toolmakers or chip-design firms. That is the strategist's first conclusion. The deepest advantage in 2026 sits where the AI economy cannot route around a supplier. TSMC holds 72.5% of foundry revenue [compute-tsmc-foundry-share] and earns a 58.7% operating margin [str-tsmc-q3-opmargin]. Its customers design for it first because it yields best at the leading edge, and as a pure foundry it never competes with them.

NVIDIA is the user's single point of failure, and the evidence agrees with the user up to a point. Its operating income was $63.7 billion on $96.2 billion of revenue in one quarter [compute-nvidia-q2fy27-opincome][compute-nvidia-q2fy27-revenue]. Its moat is CUDA: every framework and engineer is tuned to it [compute-cuda-dominates]. But that moat is software, and software can be rewritten. NVIDIA's share of AI accelerators fell from about 87% in 2024 to about 75% in 2026 [compute-nvidia-accel-share]. So NVIDIA stands fourth, below the three firms it cannot do without.

The model labs stand lower than their valuations suggest. Anthropic is valued at $965 billion [labs-anthropic-valuation] and OpenAI at $852 billion [labs-openai-valuation]. Their altitudes are 3.37 and 4.35 (*judged*). OpenAI's power is its brand: ChatGPT has more than 900 million weekly users [labs-openai-weekly-users]. Even that is eroding. ChatGPT's share of chatbot web traffic fell from 73.3% in summer 2025 [str-chatbot-share-2025] to 55.5% in August 2026, while Gemini took 25.6% [str-chatbot-share-aug2026]. Anthropic's rise is execution. Its run-rate went from about $9 billion at the end of 2025 to over $47 billion by May 2026 [labs-anthropic-run-rate-apr][labs-anthropic-run-rate-may]. The market prices that momentum. The 7 Powers ask what rivals cannot copy, and for a model lab the answer is still thin.

Memory is the year's surprise. SK Hynix earned an 80% operating margin in the third quarter [str-skhynix-q3-margin], above NVIDIA's 66%. Micron's operating income was $43.75 billion on $54.23 billion of revenue [str-micron-fq4-opincome][compute-micron-fq4-revenue]. But the judge stands them low: SK Hynix 3.87 and Micron 3.06, with no durable power found for Micron (*judged*). A shortage is not a moat. Samsung's HBM share jumped from 21% to 33% in one quarter [compute-samsung-hbm-share] while SK Hynix's fell to 50% from 64% a year earlier [compute-skhynix-hbm-share]. The market already doubts the peak: SK Hynix's shares fell 18.8% after 1 September on fears that AI investment had peaked [str-skhynix-shares-fall].

### The choke points

A choke point is a place where many rivers must pass through one actor with no substitute. The judge scored how quickly customers could replace each one (strength 1 = never, *judged*):

- **EUV lithography (ASML), 0.96.** The only supplier [compute-asml-euv-100].
- **Leading-edge logic and CoWoS packaging (TSMC), 0.76.** 72.5% of foundry revenue; Samsung has 5.9% [compute-tsmc-foundry-share][compute-samsung-foundry-share].
- **Mass manufacturing (China), 0.73.** China's output exceeds the next nine manufacturing countries combined [century-china-manufacturing-nine].
- **Export licences (the US government), 0.70.** Washington decides who abroad may buy the best chips and tools [str-h200-approval].
- **Large gas turbines (GE Vernova), 0.67.** 122 GW of backlog and slot reservations [labs-gev-gas-backlog].
- **High-bandwidth memory (SK Hynix, with Samsung and Micron), 0.64.** Sold out through 2029 [compute-skhynix-sold-out].
- **The search entry point (Google), 0.59.** 91.05% of worldwide search [century-google-search-share-2026].
- **CUDA (NVIDIA), 0.51.** The only choke point in the chain that can be eroded with software, which is why every rival attacks it.
- Outside the AI chain: the Strait of Hormuz, which carried about 20% of the world's oil before the war [economy-hormuz-oil-share], tirzepatide at Eli Lilly, and the card rail at Visa.

The pattern is plain. The physical choke points (EUV, the leading edge, memory, turbines) are harder to replace than the logical ones (CUDA, search). Physical capacity takes years and billions to copy. Software takes a team and a reason.

### The rivers, and the motives behind them

Money climbs the hillside from the buyers to the summits. All amounts are a year, annualised from the latest quarter where needed.

- **Buyers into apps, models and clouds.** Consumers spend about $458 billion a year with Apple [economy-apple-q4-revenue] and about $624 billion with Amazon's stores and services [str-amzn-q3-sales]. Businesses pay Alphabet about $510 billion, mostly for advertising and cloud [str-alphabet-q3-revenue], Meta about $258 billion [str-meta-q3-revenue], Microsoft's cloud about $256 billion [str-msft-cloud-q1fy27] and AWS about $184 billion [str-amzn-aws-q3]. The labs' rivers are still small beside these: about $50 billion of run-rate for OpenAI [labs-openai-run-rate] and over $47 billion for Anthropic [labs-anthropic-run-rate-may].
- **The clouds' capex into chips.** The four hyperscalers plan $725 billion of capex in 2026 [economy-hyperscaler-capex-2026]; with SpaceX the five plan $803-828 billion [compute-hyperscaler-capex-2026]. NVIDIA's data-centre revenue has equalled 39-47% of hyperscaler capex for two years [str-nvidia-capex-capture]. NVIDIA does not name its customers. Applying Bloomberg and Barclays' estimated shares (Microsoft 15%, Meta 14%, Alphabet 12%, Amazon 11%) [str-nvidia-customer-shares] to its annualised revenue gives rivers of $42-58 billion from each. Another $161 billion a year comes from AI clouds, AI natives, enterprises and sovereigns [compute-nvidia-q2fy27-acie].
- **Chips into fabs and memory.** Analysts expect NVIDIA to be TSMC's largest customer in 2026, at about $33 billion of its revenue against Apple's $27 billion [str-tsmc-nvidia-apple]. In the first half of 2025 a customer identified as NVIDIA paid SK Hynix 10.89 trillion won, 27% of its revenue [str-skhynix-nvidia-1h25].
- **Fabs into tools.** Taiwan took 32% of ASML's equipment sales in the third quarter and South Korea 31% [str-asml-q3-regions]. China took 23%, about €1.93 billion, buying older DUV machines while it still can [str-asml-q3-china].
- **Capital into labs and clouds.** OpenAI raised $122 billion in 2026 [labs-openai-valuation]; SoftBank alone has put in $64.6 billion [labs-softbank-openai-total]. Anthropic raised $30 billion in February and $65 billion in May [str-anthropic-series-g][labs-anthropic-valuation], and Google and Amazon committed up to $40 billion and $25 billion more [str-google-anthropic-40bn][str-anthropic-aws-100bn].
- **Labs into clouds.** OpenAI agreed to buy $300 billion of computing from Oracle over five years [labs-openai-oracle-300bn]. Anthropic committed $100 billion over ten years to AWS's own chips [str-anthropic-aws-100bn] and pays xAI $1.25 billion a month for Colossus [labs-anthropic-colossus-contract].

The rivers loop. NVIDIA offered up to $100 billion to OpenAI as its systems deploy [labs-openai-nvidia-100bn], put $2 billion into CoreWeave [str-coreweave-nvidia-2bn] and helps select customers find land and power [compute-nvidia-customer-financing]. Its purchase commitments rose from $119 billion to $279 billion in a quarter [compute-nvidia-commitments]. A supplier that finances its buyers is spending its moat to keep the market growing.

**Desire or fear.** The judge read each payer's motive from its own words and the evidence (*judged*, 0 = pure desire, 1 = pure fear). The most fearful rivers are China's purchases from ASML (0.71), bought before the door closes, and the US government's stake in Intel (0.69), bought so the country keeps one leading-edge fab. The hyperscalers' payments to NVIDIA sit in the middle (0.46-0.57): ambition and the dread of falling behind in equal measure. Mark Zuckerberg said Meta might misspend a couple of hundred billion dollars, but that "the risk is higher on the other side" [labs-zuckerberg-misspend]. Dario Amodei put the opposite fear on record: buy compute for $1 trillion of revenue that turns out to be $800 billion, and no hedge would stop bankruptcy [labs-amodei-bankrupt]. Sundar Pichai said no company would be immune if the boom broke, Google included [labs-pichai-irrationality]. The purest desire rivers are the consumers' (0.24-0.34) and SoftBank's bet on OpenAI (0.27).

### The flashpoints being fought now

Twenty-three contests of 2025-2026 stand on the hillside. The ten that matter most:

1. **CUDA against the second sources.** NVIDIA holds about 75% of accelerators [compute-nvidia-accel-share], but AMD's data-centre revenue reached a record $8.5 billion [compute-amd-q3-datacenter] and Broadcom's AI revenue $16.7 billion in a quarter [compute-broadcom-q3-ai].
2. **The buyers build their own chips.** Just over half of Google's ML compute will go to its TPUs [str-google-tpu-half]. Anthropic committed $100 billion to Amazon's Trainium [str-anthropic-aws-100bn]. Meta's AMD deal is estimated at $60 billion [str-meta-amd-60bn]. This is counter-positioning: NVIDIA cannot match a free in-house chip without cutting the margin the market pays it for.
3. **TSMC against Intel and Samsung.** TSMC 72.5%, Samsung 5.9%, SMIC 5.4% [compute-tsmc-foundry-share][compute-samsung-foundry-share][compute-smic-foundry-share]. Intel survives with the US government (9.9%) and NVIDIA ($5 billion) as owners [lineage-us-gov-intel-stake-2025][compute-nvidia-intel-investment].
4. **Google absorbs the ChatGPT shock.** Google still holds 91.05% of search [century-google-search-share-2026] and Alphabet's revenue rose 25% [str-alphabet-q3-revenue]. Google cannibalised itself rather than leave the field to a new rival, the move Kodak and Blockbuster refused to make.
5. **Anthropic against OpenAI.** Anthropic sold to businesses and bought compute on three chip platforms as revenue arrived; by April more than 1,000 customers each spent over $1 million a year [str-anthropic-business-customers]. OpenAI built on consumer scale and very large commitments.
6. **Open weights against the closed labs.** DeepSeek released the 1.6-trillion-parameter V4-Pro under the MIT licence [str-deepseek-v4], tuned to Huawei's Ascend chips [str-deepseek-v4-ascend], and was valued at $52 billion [str-deepseek-valuation-52]. Anthropic accused it of training on Claude's answers through fraudulent accounts [str-deepseek-distillation].
7. **The talent auction.** Meta offered packages reported up to $300 million [labs-meta-talent-pay], put $14.3 billion into Scale AI and hired its founder [str-meta-scale-14bn]. People are priced like assets.
8. **Export controls against China, and China's answer.** Washington approved H200 sales [str-h200-approval]; Beijing told customs to block them [compute-china-h200-customs]. NVIDIA's share of China's AI accelerators fell to zero [str-nvidia-china-zero], and Huawei expects about $12 billion of AI-chip revenue [compute-huawei-ai-revenue].
9. **HBM: SK Hynix against Samsung.** First to qualify at NVIDIA against the larger DRAM maker catching up [compute-skhynix-hbm-share][compute-samsung-hbm-share].
10. **Two old-economy contests with the same shape.** Lilly's revenue rose 39% [economy-lilly-q3-revenue] while Novo guides -2% to +2% [economy-novo-outlook-2026]. BYD sold 762,478 battery-electric cars in the third quarter to Tesla's 486,532 [economy-tesla-vs-byd-q3-deliveries], and Tesla's operating margin fell to 2.6% [economy-tesla-q3-operating-margin].

In five of these contests the challenger stands lower than the holder it is attacking: the TPU, AMD, memory, Anthropic and DeepSeek. That is what a contest in progress looks like. Momentum is on one side and the deeper power on the other.

## 2. The recursion back

### From NVIDIA to the transistor

Every step of the thread passed the advantage one layer up, from the invention to whoever owned the standard.

- **1947, Bell Labs.** Bardeen, Brattain and Shockley invented the transistor [lineage-bell-transistor-1947] inside a protected monopoly's laboratory. The invention did not stay there.
- **1957, Fairchild.** Eight engineers left Shockley's company [lineage-traitorous-eight-names] and founded Fairchild [lineage-fairchild-founded-1957]. Its planar process [lineage-fairchild-planar-1959] made chips manufacturable, and IBM bought its first 100 devices at $150 each [lineage-fairchild-first-batch-ibm]. By 1960 its alumni had founded dozens of firms, Intel and AMD among them [lineage-fairchildren-1960].
- **1968-1986, Intel.** Founded by Noyce and Moore [lineage-intel-founded-1968], it led memory with the 1103 [lineage-intel-1103-dram-1970]. When Japanese makers crushed memory margins, Grove moved Intel to microprocessors [lineage-intel-japan-dram-1983]. The IBM PC used the 8088 [lineage-ibm-pc-8088-1981], and Compaq's 386 machine made Intel the standard's supplier [lineage-compaq-386-1986]. Switching costs in software built on x86 did the rest.
- **1987, TSMC.** Taiwan's government and private investors created the first pure foundry [lineage-tsmc-founded-1987]. Competing with no customer let the fabless industry exist, NVIDIA included.
- **1993-2007, NVIDIA.** Founded in 1993 [lineage-nvidia-founded-1993], it was kept alive by a $5 million investment from Sega after a failed contract [lineage-sega-invest-5m]. The GeForce 256 was its first "GPU" [lineage-geforce-256-1999], and it bought fallen 3dfx's assets in 2000 [lineage-nvidia-acquires-3dfx-2000]. CUDA, released in 2007 [lineage-cuda-released-2007], opened the chip to general computing.
- **2012-2022, the discoveries.** AlexNet trained on two $500 gaming cards [lineage-alexnet-two-gtx580-2012][lineage-gtx580-price-500]: gamers had paid for the hardware. Google's researchers published the Transformer in 2017 [lineage-transformer-google-2017]; it trained in 3.5 days on eight GPUs [lineage-attention-bleu-8gpu]. NVIDIA gave OpenAI its first DGX-1 [lineage-dgx1-openai-2016] and bought Mellanox for $6.9 billion to own the network between GPUs [lineage-mellanox-6-9bn-2019]. Scaling laws showed loss falling as a power law in size, data and compute [lineage-scaling-laws-2020]; GPT-3 had 175 billion parameters [lineage-gpt3-175b-2020]; ChatGPT launched on 30 November 2022 [lineage-chatgpt-release-2022]. Eleven OpenAI people left to found Anthropic [lineage-anthropic-founders-from-openai].
- **The customers built their own choke point.** In 2012 Intel, TSMC and Samsung bought 23% of ASML to pay for EUV [lineage-asml-coinvest-2012]. In October 2025 NVIDIA became the first company worth $5 trillion [lineage-nvidia-5tn-2025].

Two lessons run through the thread. Advantage leaks through people and publications: the transistor, the Fairchild eight, the Transformer paper and Anthropic's founders all left the place that made them. And the winner usually sits one layer above the invention, at the standard: Fairchild over Bell Labs, Intel over Fairchild, NVIDIA's CUDA over the Transformer's authors.

### The 20th and 21st centuries: winners, and what killed the losers

- **The state as referee.** Standard Oil refined 91% of US oil in 1904 [century-standard-oil-1904-share]; when the state broke it up in 1911, rivals had already cut it to 64% [century-standard-oil-1911-share]. AT&T was divided in 1984 [century-bell-divestiture-1984]. Microsoft, with more than 90% of workplace PCs [century-msdos-90pct], was found guilty of monopolisation in 2000 [century-microsoft-guilty-2000].
- **Outsourcing the part that matters.** IBM made 70% of the world's computers in the 1960s and 1970s [century-ibm-1960s-share]. It took its PC's processor and operating system from outside, and in 1993 it lost $8 billion [century-ibm-1993-loss]. Intel and Microsoft kept the profit.
- **Ecosystem beats product.** Nokia had 40.4% of handsets in 2007 [century-nokia-2007-share] and bet on Windows Phone in 2011 [century-nokia-windows-phone-2011]. Today Android and iOS hold 72.36% and 27.61% [century-mobile-os-share-2026].
- **Declining the cheap option.** Yahoo declined to buy Google for $1 million [century-yahoo-google-offer] and sold its core business for $4.83 billion in 2016 [century-yahoo-verizon-2016]. Blockbuster turned down Netflix for $50 million [century-blockbuster-declined-netflix] and went bankrupt in 2010 [century-blockbuster-2010-bankruptcy].
- **Process beats size.** Walmart passed Sears by 1989 [century-walmart-passes-sears]; Sears went bankrupt in 2018 [century-sears-bankruptcy-2018]. GM was the largest carmaker for 77 years until Toyota passed it in 2008 [century-gm-lost-title-2008], and needed $17.4 billion of bailouts [century-gm-bailout-2009].
- **Leverage and opacity.** GE was the world's most valuable company in 2000 at $601 billion [century-ge-2000-market-cap] and left the Dow in 2018 [century-ge-removed-dow-2018]. Lehman failed with $639 billion of assets [century-lehman-assets].
- **Buying the threat.** Facebook bought Instagram for about $1 billion [century-instagram-acquired] and WhatsApp for $19 billion [century-whatsapp-2014].

Every 2026 summit faces one of these killers. CUDA faces the ecosystem test, now from the buyers' own chips. Google faces a new model that cannibalises its old one. OpenAI and Oracle face the leverage test. NVIDIA faces the IBM question in reverse: it depends on TSMC and the memory makers for the parts that matter.

## 3. The archetypes, the biosphere, and the pattern that repeats

**The Mongols won with a system, not a stirrup.** The stirrup was Chinese, from the 4th century [archetypes-stirrup-chinese-origin]. The Mongol advantage was the bundle: three or four horses per rider [archetypes-mongol-horses-per-soldier], a bow small enough for the saddle and quivers of 60 arrows [archetypes-mongol-bow-quivers], and a tribal society rebuilt as a meritocracy [archetypes-mongol-meritocracy]. They absorbed the defeated: prisoners were driven forward in sieges [archetypes-mongol-prisoners-driven-forward], and a thousand Chinese engineer squads marched with Hulagu [archetypes-mongol-chinese-engineers]. By 1227 the empire was twice the size of Rome's [archetypes-mongol-empire-extent].

**Alexander did the same with a line and a wedge.** His long-pike phalanx outclassed the Persians' weapons [archetypes-alexander-phalanx-edge], and at Gaugamela he drove a wedge into a weakened centre [archetypes-gaugamela-wedge]. He then gave Persians command posts, which his Macedonians resented [archetypes-alexander-persian-officers].

**Napoleon combined mass, organisation and fire, and took the crown himself.** The levy of 1793 called about 300,000 men [archetypes-napoleon-levy-1793]; six corps under marshals marched apart and fought together [archetypes-napoleon-grande-armee-corps] to win at Jena in 1806 [archetypes-napoleon-jena-1806]; massed artillery became the spearhead [archetypes-napoleon-massed-artillery]. He crowned himself rather than receive the crown from the Church [archetypes-napoleon-crowned-himself]. Then the coalitions beat him at Leipzig and Waterloo [archetypes-napoleon-leipzig-1813][archetypes-napoleon-waterloo-1815], and the Concert of Europe kept a balance until 1914 [archetypes-vienna-concert-of-europe].

**Germany twice had the weight and twice overreached.** Its steel passed Britain's in 1893 [archetypes-ww1-german-steel-1893]. Its production depended on resources outside its control [archetypes-ww2-germany-resource-dependence]. The Allies produced 1,043 million tonnes of crude oil to the Axis's 66 million [archetypes-ww2-crude-oil], and American industry made nearly two thirds of the Allies' munitions [archetypes-ww2-us-two-thirds-munitions]. In 1950 the United States had 6.0% of the world's people and 27.3% of its economic activity [archetypes-us-1950-share-of-activity].

**The biosphere sets the arithmetic.** Only about 10% of energy passes up each trophic level [archetypes-trophic-ten-percent], so the top predator is few and depends on a broad base; lynx rise and fall with the hares [archetypes-lynx-hare-cycle]. When the environment changed at the end of the Cretaceous, three quarters of species died [archetypes-kpg-three-quarters]. The dinosaurs could not shelter [archetypes-kpg-dinosaurs-no-shelter]; every major mammal lineage survived [archetypes-kpg-mammals-survived].

**The pattern.** A winner builds a system advantage that rivals cannot copy piece by piece. It grows until it overreaches or until its base shrinks. Then a coalition forms against it, or the environment changes, and balance returns.

The 2026 hillside shows the same moves:

- **The system advantage.** NVIDIA's is a system, not a chip: CUDA, networking and the libraries, sold whole.
- **The coalition.** NVIDIA's four largest direct customers are 61% of its revenue [str-nvidia-top-customers], and those same buyers are building TPUs, Trainium and MTIA, and buying AMD. That is the Sixth Coalition in silicon.
- **The predator's base.** Like the lynx, the summit lives on the base below it: the buyers' spending of $725 billion [economy-hyperscaler-capex-2026]. If the hares fall, so does the lynx.
- **The new environment.** China has closed its market to NVIDIA [str-nvidia-china-zero]. Power, not chips, may become the binding limit: the IEA projects data centres will use 945 TWh in 2030 against 415 TWh in 2024 [labs-iea-datacentre-2030]. The firms that survive an environmental shift are the ones that can shelter, as the mammals did: those with cash, powered sites and their own workloads.

## 4. Predictions to 2030

### The most likely course

The judge's durability answers project each altitude to 2030 (*judged*). The strategist's reading of them:

1. **The physical choke points deepen.** ASML reaches the top of the scale (7.0), TSMC rises to 6.63 and KLA to 6.65, Arm to 6.14 (*judged*). EUV, the leading edge and process control cannot be copied by 2030, and every scenario except a Taiwan disruption feeds them.
2. **NVIDIA holds but does not widen.** Its 2030 altitude is about flat, 5.67 (*judged*). CUDA keeps most of the market, but share drifts as the buyers' own chips grow: Google's TPU shipments are projected at 4.3 million in 2026 and more than 35 million in 2028 [str-google-tpu-half]. NVIDIA stays the largest chip firm and becomes less singular.
3. **Memory reverts.** SK Hynix (3.78) and Micron (2.94) drift down by 2030 (*judged*). Capacity arrives, Samsung catches up, and an 80% margin invites exactly that.
4. **The leveraged builders are the weakest link.** CoreWeave falls the furthest of any firm, to 1.85 (*judged*). It carries about $35.1 billion of debt [str-coreweave-debt] against a market value of $25.59 billion [str-mcap-coreweave]. Oracle's $664 billion backlog [labs-oracle-rpo] rests largely on one customer's $300 billion contract.
5. **The labs' moats stay thin.** OpenAI drifts down to 4.12 and Anthropic up only to 3.55 (*judged*). Open weights and the clouds' own models press on prices; the labs that own a channel (ChatGPT's users) or a workflow (coding agents) keep the most.
6. **The cash-flow clouds hold the middle.** Alphabet (5.79), Microsoft (5.64) and Amazon (5.42) rise (*judged*). They own the customers, fund capex from operations, and earn whichever lab wins: Microsoft's CFO expects to stay free-cash-flow positive at about $170 billion of capex [str-msft-fcf-positive].
7. **China's separate stack hardens.** Huawei rises to 5.96 inside its protected market (*judged*), with orders from Alibaba, ByteDance and Tencent [str-huawei-orders].
8. **Outside the chain, process beats a head start.** BYD passes Tesla (4.83 against 3.57) and Novo Nordisk falls to 2.45 while Lilly holds 4.88 (*judged*). Both are the first-mover's fate the 20th century kept repeating.

### Scenario by scenario

Each scenario moves every actor by the judge's answer to "how would this change its position" (*judged*, the step in altitude in brackets).

- **Compounding demand** (AI revenue keeps up; capex passes $1 trillion in 2027). Everyone in the chain rises; the scarcest rise most: Constellation (+1.25), TSMC (+1.05), Broadcom, Vertiv and Samsung (about +1.0). The labs gain little against each other. Mechanism: shortage pricing at every choke point. Signposts: the labs' combined run-rate passing $150 billion; 2027 capex guidance above $1 trillion; HBM and CoWoS lead times lengthening.
- **Capex digestion** (capex cut 30-40% for 18 months). The losers are the leveraged and the committed: CoreWeave (-1.61), OpenAI (-1.13), Oracle (-1.12), Micron (-0.89) and NVIDIA (-0.85). Berkshire gains (+0.67) as the buyer of last resort; Apple, Visa, Walmart and Lilly are barely touched. Mechanism: commitments and debt become liabilities when revenue stalls, as in the telecom bust. Signposts: a hyperscaler cutting guidance; GPU rental and HBM prices falling; memory shares falling on record earnings, as SK Hynix's already did [str-skhynix-shares-fall].
- **The power wall** (grid connections of four years or more; turbines sold out to 2030). Advantage moves from chips to watts: Constellation (+1.60), xAI (+1.34), whose Colossus has its own power, GE Vernova (+0.95) and Siemens Energy (+0.74). OpenAI (-0.57) and CoreWeave (-0.52) lose time money cannot buy. Signposts: GE Vernova's gas backlog passing 130 GW [labs-gev-gas-backlog]; grid waits in Virginia and Texas; hyperscalers buying power plants.
- **A Taiwan disruption** (TSMC's Taiwan output halted for six months or more). TSMC (-1.58), Apple (-1.34), Foxconn (-1.33), NVIDIA (-1.29) and the compute buyers fall; Intel (+1.42), SMIC (+1.13) and Samsung (+1.02) rise as the only other fabs. Mechanism: the deepest choke point has no substitute for years. Signposts: exercises around Taiwan and shipping insurance; the ramp of TSMC's Arizona and Japan fabs; Intel winning outside customers.
- **Models commoditised** (open weights match the frontier at a tenth of the price). DeepSeek gains most (+1.23); OpenAI (-1.22), Anthropic (-1.05) and CoreWeave (-0.97) lose. The clouds and Apple barely move: they sell whichever model wins. Signposts: open models' benchmark gap; API prices per million tokens; the labs' gross margins.
- **A regulatory clampdown** (licensing, liability and antitrust remedies in the US and EU). Meta (-0.85), Alphabet (-0.80), OpenAI (-0.75) and NVIDIA (-0.70) lose; DeepSeek (+0.93), outside the rules, and Palantir (+0.70), built for government compliance, gain. Signposts: EU AI Act fines; US frontier-model bills; remedies in the search case.
- **US-China decoupling** (no tools, chips, servicing or software across the line; rare earths restricted). A full cutoff hurts China's fabs more than protection helps them, at least for two or three years: CXMT (-1.26) and SMIC (-1.06). Tencent (-0.94), Foxconn (-0.82), Apple (-0.72) and BYD (-0.72) lose; Intel (+0.77) and TSMC (+0.47) gain as the West's fabs. Signposts: rules on DUV servicing; China's rare-earth licences; Apple's China sales.

The environment already leans unfriendly at the edges. The Fed held at 3.75-4% with two members voting to raise [economy-fed-hold-oct2026][economy-fed-dissent-hike], and inflation remains elevated [economy-fed-inflation-statement]. Brent is expected near $98 [economy-eia-brent-2026]. The ten largest firms are 39.1% of the S&P 500 [economy-sp500-top10-weight], and AI took 82.7% of US venture money [economy-vc-ai-share]. One bet carries the market, so a fall would be correlated rather than diversified. The one friendly surprise is fiscal: the struck-down tariffs may return up to $175 billion to importers [economy-tariff-refunds-wharton].

### The signposts that matter most

1. Hyperscaler capex guidance for 2027: above $1 trillion means compounding; a cut means digestion.
2. The labs' revenue against their commitments: OpenAI's run-rate against its $300 billion Oracle contract [labs-openai-oracle-300bn], and Anthropic's against its own warning [labs-amodei-bankrupt].
3. NVIDIA's share of accelerators against the buyers' own chips [compute-nvidia-accel-share][str-google-tpu-half].
4. HBM contract prices and Samsung's share [compute-samsung-hbm-share].
5. Turbine backlogs and grid waits [labs-gev-gas-backlog].
6. CoreWeave's and Oracle's cost of borrowing [str-coreweave-debt].
7. Taiwan Strait activity, and China's rules on chips and rare earths [compute-china-h200-customs].

## 5. How these predictions were made, and their limits

**Facts.** Six research clusters and the strategist gathered 370 facts. Each passed the truth gate: the quote was found verbatim on a page read on 7 November 2026, its values sit inside the quote, and Jev judged the claim supported at 0.6 or more. Market caps come from companiesmarketcap.com on a Saturday, so they are Friday's closing prices.

**Judgments.** For every actor in the arena Jev scored the 7 Powers (0-4), chose a primary power, scored the triad (information, execution, environment), judged durability to 2030 and answered each scenario. It also judged each river's motive, each choke point's substitutability and each flashpoint's deciding power. Altitude is composed in code: A = s1 + s2/2 + s3/4 over the three strongest powers. A scenario moves an actor by 0.9 per step of the judge's answer.

**The review.** The strategist checked every judgment against the evidence. Where an answer was weak or contradicted the facts, the fix was better evidence, not an override: chatbot traffic shares for OpenAI, delivery scale for Amazon, the backlog for GE Vernova, the mechanism for BYD, the database base for Oracle. After that no override was needed. The record is in review.json.

**Limits.**

- Altitude is a judgment of durable advantage made by a model from sourced facts. It is not a price, and it is not a forecast of returns. A firm can be large and low, as Tesla is, or small and high, as KLA is.
- Several rivers are estimates. NVIDIA does not name its customers, so the hyperscalers' rivers use 2025 analyst shares [str-nvidia-customer-shares]. OpenAI's split between consumers and businesses uses its March statement that enterprise was about 40% of revenue [str-openai-enterprise-share]. The SK Hynix river is from 2025. Some large commitments are not drawn because no term is disclosed: OpenAI's $250 billion with Azure [labs-microsoft-openai-stake] and NVIDIA's up to $100 billion in OpenAI.
- No fetchable source gives the data centres' power bill, so the energy band has few rivers.
- Run-rates are not booked revenue. Anthropic's 2025 revenue was $4.585 billion [labs-anthropic-revenue-2025], far below its 2026 run-rate.
- Scenarios are conditional. The judge answers "if this happened"; it does not say how likely each one is. The strategist's view of likelihood: digestion and the power wall are the live risks of 2027; Taiwan is the tail.
- The judge knows the world only up to its training and the evidence given. A model can be confidently wrong. The scores are evidence, not authority.
