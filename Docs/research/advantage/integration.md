# Integration: who works in which stratum

As of 7 November 2026. Strata, top to bottom: consumer, models, clouds, chips, fabs, tools, energy, materials, state. This cluster covers the first six plus energy for Tesla. Raw materials and government belong to the `materials` cluster. Every number below is a verified fact; ids are in brackets.

## The pattern

Three kinds of integrator appear.

1. **Full stack, own use.** Alphabet, Microsoft, Amazon and Meta each hold a consumer layer, a model, a cloud and a chip. Meta is the case that does not sell its cloud: it builds data centres for itself (capex guidance of $140 billion, Hyperion planned at 5 GW) [labs-meta-capex-2026, buildout-meta-hyperion-5gw] and now makes its own chips, MTIA 300 deployed and 400, 450, 500 to follow [integration-meta-mtia300-deployed, integration-meta-mtia-roadmap]. Its audience is 3.64 billion daily active people [integration-meta-dap].
2. **Chips pushing up.** NVIDIA owns the accelerator (75 % of AI accelerators, 2026E) [compute-nvidia-accel-share] and has added a CPU, switches, and open models [integration-nvidia-vera-cpu, integration-nvidia-spectrum6, integration-nvidia-nemotron]. It stays out of the cloud and consumer strata but holds $29 billion of cloud commitments [balancea-nvidia-commit-cloud].
3. **Consumer pulling down.** Apple owns 2.5 billion active devices [integration-apple-active-devices] and 22 % of smartphones [integration-apple-smartphone-share] but rents the model: Siri on a custom Gemini, chosen on price, as Bloomberg reported [integration-apple-gemini-siri]. Tesla designs its AI5 chip and has Samsung and TSMC make it [integration-tesla-ai5-dual-source].

## Company by company

- **Alphabet.** The most complete stack: Search, YouTube, Android and Chrome [integration-google-services-scope]; the Gemini app at one billion users [integration-google-gemini-app-users]; Google Cloud at $30.7 billion a quarter with a $561 billion backlog [labs-google-cloud-q3, buildout-google-cloud-backlog]; and the TPU, now also sold as systems [integration-google-tpu-systems-sales]. A strategist should read the TPU sale as the move from internal chip to merchant chip: Alphabet is entering NVIDIA's stratum.
- **Microsoft.** States its model outright: chips, models and applications together are a competitive advantage [integration-microsoft-integrated-statement]. LinkedIn (1.4 billion members), GitHub, Windows and Xbox are the horizontal families [integration-microsoft-linkedin-members, integration-microsoft-devices-consumer-revenue]. Cloud revenue $63.9 billion in the quarter and $677 billion of contracted backlog [str-msft-cloud-q1fy27, balancea-microsoft-commit-rpo].
- **Amazon.** Retail ($112.9 billion North America, $43.1 billion International in the quarter) [integration-amazon-north-america-sales, integration-amazon-international-sales], AWS ($46.0 billion) [str-amzn-aws-q3] and a chips business at a $20 billion run rate with $225 billion of Trainium commitments [integration-amazon-chips-run-rate, integration-amazon-trainium-commitments]. The run rate covers Graviton, Trainium and Nitro, not Trainium alone.
- **Meta.** See above; Reality Labs shows the cost of the device layer ($80 billion cumulative loss) [weapons-reality-labs-cumulative].
- **Apple.** Strong consumer stratum, absent from models and clouds as an owner.
- **NVIDIA.** See above. Its SpaceXAI customer will deploy Vera CPUs [integration-nvidia-spacexai-vera].
- **Tesla.** Cars, 1.74 million FSD subscriptions, own chip design, energy storage of 13.7 GWh in a quarter [integration-tesla-fsd-subs, integration-tesla-energy-storage]. R&D rose 69 % to $2.76 billion [integration-tesla-rd].
- **SpaceX with xAI.** Starlink 10.3 million subscribers, X 550 million monthly users, Grok about 117 million [integration-spacex-starlink-subs, integration-spacex-x-mau, integration-spacex-grok-mau]; Colossus operating at 1.7 GW [buildout-spacex-operating-17gw]. User counts are secondary readings of the S-1.
- **Samsung.** The only company in consumer, memory and foundry at once: 23 % of smartphones [integration-samsung-smartphone-share]; Device Solutions earned KRW 106.5 trillion in the quarter [integration-samsung-ds-q3]; foundry share 5.9 % [compute-samsung-foundry-share].
- **Intel.** Design and foundry under one roof: Data Center and AI $7.3 billion, Foundry $6.7 billion [integration-intel-dcai-revenue, integration-intel-foundry-revenue].
- **Broadcom.** Chips ($20.8 billion) and VMware-era software ($8.75 billion) in one company, a horizontal acquisition roll-up [integration-broadcom-semis-revenue, integration-broadcom-infra-software].
- **Oracle.** Cloud infrastructure $7.4 billion up 121 %, SaaS $4.2 billion, on-premises software $5.5 billion falling [integration-oracle-iaas-revenue, integration-oracle-saas-revenue, integration-oracle-software-revenue]; the legacy layer is shrinking into the cloud.
- **Alibaba.** Chips and cloud merged into one segment (RMB48,437 million), Qwen folded into a consumer app with e-commerce [integration-alibaba-ai-cloud-compute, integration-alibaba-tHead-merged, integration-alibaba-qwen-app]; Zhenwu M890 has 144GB of HBM [integration-alibaba-zhenwu-m890].
- **Huawei.** Phones and HarmonyOS (17 % of mainland China) plus Ascend chips [integration-huawei-harmonyos-share, integration-huawei-ascend-models, compute-huawei-ai-revenue]; 2025 revenue above RMB 880 billion [integration-huawei-revenue-2025].
- **Tencent.** 1,439 million Weixin and WeChat users, the Hy3 model, and RMB 59.3 billion of quarterly capex building the cloud [integration-tencent-weixin-mau, integration-tencent-hy3, integration-tencent-capex-q2].

## What a strategist would act on

- The integrated four (Alphabet, Microsoft, Amazon, Meta) all now make their own chips; the exposed party is the pure chip seller, but the chip makers are moving up (NVIDIA CPUs, networking, models). The contest is at the chip and cloud strata.
- Apple's choice to rent a model is the weak link in a consumer-heavy position; Alphabet is both its rival and its supplier.
- Samsung and Intel are the only two with both design and fabrication; both are the customer-diversification option for chip designers (Tesla uses Samsung).
- Legacy software (Oracle, VMware) is being absorbed into cloud revenue.

## Second pass: the labs, Apple's silicon, power, and family numbers

**The labs integrate down the stack.** OpenAI holds the largest consumer surface (ChatGPT, more than 900 million weekly users) [moats-openai-chatgpt-weekly-users], bought Jony Ive's io for devices at a reported $6.5 billion [weapons-acq-openai-io], and is building its own cloud through Stargate (up to $500 billion planned) [labs-stargate-500bn] and its own chips (10 GW of accelerators with Broadcom, a warrant for up to 160 million AMD shares tied to 6 GW) [labs-openai-broadcom-10gw, labs-openai-amd-warrant]. Anthropic works in three strata: Claude Code (run-rate over $2.5 billion in February) [integration-anthropic-claude-code-runrate], its models ($47 billion run-rate in May) [labs-anthropic-run-rate-may], and its own data centres ($50 billion with Fluidstack) [labs-anthropic-fluidstack-50bn]. Both still rent most of their compute, so a strategist should read their cloud and chip entries as a move that is announced, not finished.

**Apple is integrated where it matters.** It designs its own silicon (the M5's 10-core GPU has over 4x the peak GPU compute of the M4) [integration-apple-m5-gpu], is about $27 billion, about 18%, of TSMC's 2026 revenue (an analyst estimate) [str-tsmc-nvidia-apple], and builds the servers for Private Cloud Compute in its own Houston facility, inside a $600 billion US commitment [integration-apple-houston-600bn]. Only the model layer is rented (Gemini) [integration-apple-gemini-siri].

**Amazon and Huawei fill their holes.** Amazon has its own Nova models (four new in December 2025) [integration-amazon-nova-four-models] and a stake in Anthropic, capped at 12% [owners-amazon-anthropic-cap]. Huawei Cloud's external revenue fell 3.5% to CNY 32.16 billion in 2025 [integration-huawei-cloud-revenue] while its Pangu models reached more than 500 scenarios [integration-huawei-pangu-scenarios].

**Energy is the new integration frontier.** Microsoft: 20-year Crane restart, about 835 MW [integration-microsoft-crane-ppa]. Amazon: 1,920 MWe from Talen's Susquehanna plant to 2042 [integration-amazon-talen-susquehanna], plus 190 MW at Calvert Cliffs [integration-amazon-calvert-cliffs]. Meta: 1,121 MW from Clinton [integration-meta-clinton-ppa]. Google: up to 500 MW with Kairos [integration-google-kairos], 890 MW of new nuclear with Constellation [integration-google-constellation-890mw] and the $4.75 billion purchase of Intersect [integration-google-intersect]. xAI: 41 permitted gas turbines, about 1.2 GW, for Colossus [integration-xai-southaven-turbines]. Constellation (market cap $88.55 billion) [str-mcap-constellation] and GE Vernova sit under all of them. The pattern: the hyperscalers are buying existing nuclear output, Google is buying a developer, and xAI is generating its own power.

**Horizontal families, with numbers.**
- Broadcom's acquisitions: VMware (about $69 billion) [integration-broadcom-vmware-69bn], CA Technologies (nearly $19 billion) [integration-broadcom-ca-19bn], Symantec's enterprise business ($10.7 billion) [integration-broadcom-symantec-107bn]. Roughly $99 billion of software bought by a chip company.
- Alphabet: Search & other $63.35 billion and YouTube ads $11.2 billion in Q3 2026 [integration-google-search-youtube-q3].
- Microsoft: the Agents and Infra segment, $76.8 billion, with Azure up 48% [integration-microsoft-agents-infra]; Maia 200 has over 140 billion transistors [integration-microsoft-maia200-transistors].
- Meta: Facebook, WhatsApp and Instagram each at 3 billion monthly users [integration-meta-three-billion-apps], two of them bought (Instagram about $1 billion, WhatsApp about $19.3 billion) [weapons-acq-instagram, weapons-acq-whatsapp].
- NVIDIA: it now reports Data Center ($89.0 billion in Q2 FY27) and Edge Computing ($7.2 billion) [integration-nvidia-data-center-q2fy27, integration-nvidia-edge-q2fy27], not networking and gaming separately. Models are sized too: Nemotron 3 Super has 120 billion parameters [integration-nvidia-nemotron-super-params]; Tencent's Hy3 has 295 billion [integration-tencent-hy3-params].

## Not verified

NVIDIA networking and gaming revenue (no longer reported separately), Meta's Llama, Windows and Android user counts, Taobao users, a size for Tesla's AI5, Meta's MTIA, Alibaba's Zhenwu and Samsung's HBM4, the Houston server plant's capacity, Qwen's current user count. Left out rather than guessed. Meta's per-app 3 billion rests on a secondary report.

## Sources

Alphabet, Meta, Microsoft, Apple, Amazon, Oracle, Tencent and Intel releases and reports; NVIDIA Q2 FY2027 release; TrendForce; Counterpoint and Omdia via press; Motley Fool and TNW on Amazon; SiliconANGLE; Bloomberg via Unite.AI; Electrek; Drive Tesla Canada; Digital Applied and Quashbugs on the SpaceX S-1.

Verified: 79 of 79 facts (gate run 7 Nov 2026), 25 of them added in the second pass.
