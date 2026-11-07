# Story one: the American century to the AI revolution, 1945-2026

Cluster `story-ai`, as of 7 November 2026. Facts are in `story-ai.json`; ids in brackets are either new (`story-ai-...`) or reused from earlier rounds in `model.json`. Every fact passed the truth gate.

The thread through the story: an advantage is rarely kept. It is invented in one body, leaks through people and publication into another, and is finally held by whoever owns the scarce thing everyone else must buy. Nine periods, sixteen moments.

## 1. The American century (1945-1971)

When the war ended the United States was responsible for half of global industrial output and held 80 percent of the world's gold [story-ai-us-half-industrial] [story-ai-us-gold-80]. (Another source says two-thirds of the gold at Bretton Woods [story-ai-bretton-gold-two-thirds]; both are recorded.) It spent US$13.3 billion on 17 European countries through the Marshall Plan [story-ai-marshall-plan]. In 1950 its GDP was 1,456 billion 1990 dollars, against 510 for the Soviet Union and 161 for Japan [story-ai-gdp-1950]. The rival was the world's second-largest economy with the largest standing military [story-ai-ussr-second-economy]. Henry Luce had named the idea in Life on 17 February 1941 [story-ai-luce-1941].

Advantage type: environment (an undamaged continent), then capital (gold, the dollar), then legitimacy (the rules it wrote). A strategist's note: the openness to risk the user describes needs this base; the technology story below is what that base paid for.

## 2. The transistor and the integrated circuit (1947-1959)

Bardeen, Brattain and Shockley invented the transistor at Bell Labs in 1947 [lineage-bell-transistor-1947] and shared the 1956 Nobel Prize [lineage-nobel-1956]; eleven Nobel Prizes in all have gone for work done there [story-ai-bell-nobels]. The Bell System, a regulated monopoly of AT&T, 24 operating companies and Western Electric [century-bell-breakup], paid for the research; divestiture on 1 January 1984 ended it [century-bell-divestiture-1984].

Shockley Semiconductor was founded in 1955 [lineage-shockley-lab-1955]. Eight engineers left [lineage-traitorous-eight-names]; Fairchild began on 18 September 1957 [lineage-fairchild-founded-1957], to make silicon transistors when germanium was the norm [story-ai-fairchild-silicon]. The eight were Blank, Grinich, Hoerni, Kleiner, Last, Moore, Noyce and Roberts [story-ai-fairchild-eight-names]. Kilby built an integrated circuit at Texas Instruments in 1958 [story-ai-kilby-1958]; Noyce invented the monolithic one in 1959 [lineage-noyce-monolithic-ic-1959] on Fairchild's planar process [lineage-fairchild-planar-1959]. The first batch of 100 devices went to IBM at $150 each [lineage-fairchild-first-batch-ibm]. In 1965 Moore, then at Fairchild, was asked for a ten-year forecast [story-ai-moore-1965].

Who held the advantage: the Labs invented, Fairchild commercialised. How it passed: by defection of people, which is why the talent source matters more than the patent.

## 3. Intel and the microprocessor (1968-1995)

Intel was founded in 1968 by Moore and Noyce [lineage-intel-founded-1968]. The silicon-gate process let one chip hold 2,300 transistors [story-ai-intel-4004-transistors]; the 4004 shipped on 15 November 1971 for the calculator maker Busicom [lineage-intel-4004-1971] after Faggin moved from Fairchild [lineage-faggin-left-fairchild-1970]. In 1975 Moore revised his law to a doubling every two years, 41 percent a year [story-ai-moore-1975-two-years].

Japanese memory makers squeezed Intel in 1983 and Andy Grove turned it to microprocessors [lineage-intel-japan-dram-1983]. The IBM PC (12 August 1981) used an Intel 8088 [lineage-ibm-pc-8088-1981]; Compaq's 1986 80386 clone beat IBM and made Intel the key supplier [lineage-compaq-386-1986]. Only then did the microprocessor become Intel's main business [story-ai-intel-pc-primary]. Intel's revenue reached $25.07 billion in 1997 and $33.72 billion in 2000 [story-ai-intel-rev-1997] [story-ai-intel-rev-2000]; its market value was $274.43 billion at the end of 1999 [story-ai-intel-mcap-1999].

Advantage: switching costs (the x86 instruction set that all PC software needed). That same lock-in is what the story later watches Nvidia rebuild with CUDA.

## 4. The foundry and the GPU (1987-2012)

TSMC was founded in 1987 as a joint venture of Taiwan's government and private investors [lineage-tsmc-founded-1987]; only Philips would put in, $58 million for 27.6 percent [story-ai-tsmc-philips]. Morris Chang built the first dedicated foundry [story-ai-tsmc-chang-first-foundry]. Its revenue was $1.34 billion in 1997, $17.46 billion in 2012 and $73.86 billion in 2022 [story-ai-tsmc-rev-1997] [story-ai-tsmc-rev-2012] [story-ai-tsmc-rev-2022]. Today it holds 72.5 percent of top-10 foundry revenue [compute-tsmc-foundry-share].

Nvidia began in 1993 [lineage-nvidia-founded-1993] with $40,000 in the bank [story-ai-nvidia-founding-capital]. In 1996 it cut staff from 100 to 40 to bet on one product [story-ai-nvidia-1996-layoff]. Sega's investment kept it alive [lineage-sega-invest-5m]. The GeForce 256 (1999), marketed as the first GPU [lineage-geforce-256-1999], carried 17,000,000 transistors and was made by TSMC [story-ai-geforce256-transistors]. In December 2000 Nvidia agreed to take 3dfx's assets [lineage-nvidia-acquires-3dfx-2000]. CUDA was released in 2007 [lineage-cuda-released-2007]. Nvidia's market value: $0.96 billion at the end of 1999 [story-ai-nvidia-mcap-1999], $13.15 billion in 2006 [story-ai-nvidia-mcap-2006], $7.66 billion in 2012 [story-ai-nvidia-mcap-2012]; revenue $0.37 billion in 1999 and $4.28 billion in 2012 [story-ai-nvidia-rev-1999] [story-ai-nvidia-rev-2012].

Advantage: process power in shipping GPUs fast, then technology lock-in with CUDA, resting on TSMC, which in turn took customers from the integrated makers.

## 5. Deep learning finds its engine (2012-2017)

AlexNet, by Krizhevsky, Sutskever and Hinton, was trained on two GTX 580s [lineage-alexnet-two-gtx580-2012] costing $500 each [lineage-gtx580-price-500]. It has 60 million parameters [story-ai-alexnet-params] and won ImageNet with a top-5 error of 15.3 percent, more than 10.8 points ahead [story-ai-alexnet-error]. In 2016 Nvidia gave its first DGX-1 to OpenAI, cutting a six-day run to two hours [lineage-dgx1-openai-2016]. Nvidia's value rose to $117.26 billion by the end of 2017 [story-ai-nvidia-mcap-2017]; revenue to $9.71 billion [story-ai-nvidia-rev-2017].

"Attention Is All You Need" was submitted on 12 June 2017 [lineage-attention-transformer-2017] by eight Google authors [story-ai-transformer-authors]. Recurrent models could not be parallelised onto GPUs; attention could [story-ai-transformer-gpu-parallel]. It reached 41.8 BLEU in 3.5 days on eight GPUs [lineage-attention-bleu-8gpu]. It has been cited more than 150,000 times [story-ai-transformer-citations], and all eight authors left Google [story-ai-transformer-authors-left]. Google then had $110.85 billion of revenue and a $729.45 billion market value [story-ai-google-rev-2017] [story-ai-google-mcap-2017], and still did not hold the advantage it invented.

## 6. OpenAI scales it (2015-2022)

OpenAI was founded in December 2015 with $1 billion pledged [story-ai-openai-pledge-2015]. Parameters then went 117 million (GPT-1, 2018) [story-ai-gpt1-params], 1.5 billion (GPT-2, 2019) [story-ai-gpt2-params], 175 billion (GPT-3, 2020) [lineage-gpt3-175b-2020], following the scaling laws of 2020 [lineage-scaling-laws-2020]. On one GPU GPT-3 would have cost about $4.6 million and 355 years [story-ai-gpt3-cost]: only a firm with a cluster could run the experiment. OpenAI became a capped for-profit in 2019 [story-ai-openai-for-profit-2019]; Microsoft put in $1 billion [story-ai-microsoft-openai-2019] and licensed GPT-3 exclusively in September 2020 [story-ai-gpt3-microsoft-licence].

Advantage: not the architecture (public) but the conviction to spend on scale, and the capital and compute of a partner. Intelligence as compression [lineage-hutter-compression-ai] gets better with size.

## 7. The AI revolution (2022-2026)

ChatGPT was released on 30 November 2022 [lineage-chatgpt-release-2022], reached 100 million monthly users in two months and 900 million weekly by February 2026 [lineage-chatgpt-users-2026]. Microsoft added $10 billion in January 2023 [story-ai-microsoft-openai-2023]. OpenAI was valued at $500 billion in October 2025 [story-ai-openai-500bn-2025] and $852 billion in 2026 [labs-openai-valuation]; Anthropic, founded by eleven who left OpenAI [lineage-anthropic-founders-from-openai], is valued at $965 billion [labs-anthropic-valuation].

Nvidia reached $1 trillion in 2023 [story-ai-nvidia-1tn-2023], went from $1 to $2 trillion in 180 days [lineage-nvidia-2tn-180-days], and passed $5 trillion on 29 October 2025 [lineage-nvidia-5tn-2025]. Its hardware: 80 billion transistors on the GH100 in 2022 and 208 billion on the GB200 in 2024 [story-ai-gh100-transistors] [story-ai-gb200-transistors]. In November 2026 it is worth $4.506 trillion [compute-nvidia-marketcap], with trailing revenue of $302.96 billion [story-ai-nvidia-rev-ttm], a 75 percent accelerator share [compute-nvidia-accel-share] and $279 billion of purchase commitments [compute-nvidia-commitments]. Its customers' capex is forecast at $803 to $828 billion for 2026 [compute-hyperscaler-capex-2026], about 45 percent of which NVIDIA captures [str-nvidia-capex-capture]. Alphabet's value is $3.490 trillion and Microsoft's $3.940 trillion [story-ai-google-mcap-2026] [story-ai-microsoft-mcap-2026].

The counter-moment: on 27 January 2025 DeepSeek's cheaper training cost Nvidia over $589 billion in a day [entry-deepseek-nvidia-record-loss]; the choke point survived [lineage-nvidia-5tn-2025].

## What a strategist would predict

- The American base (capital, rules, openness) is the permanent condition; each technological layer is a short-lived lead that leaks through people (the traitorous eight, the eight authors, eleven to Anthropic).
- What lasts is the layer that others must buy: Intel's x86 in 1981-2012, TSMC's foundry now, Nvidia's CUDA and CoWoS allocation now [moats-nvidia-cuda-developers] [moats-nvidia-cowos-share-2026].
- Threats are the old ones: Japanese memory makers squeezing Intel in 1983 has its echo in a cheaper model (DeepSeek), and TSMC's single-country location is the exogenous risk.

## Sources

Wikipedia (American Century, Pax Americana, Economy of the Soviet Union, Marshall Plan, Bretton Woods, Bell Labs, Fairchild, Jack Kilby, Moore's law, Intel, Intel 4004, TSMC, History of Nvidia, Nvidia, Transistor count, Attention Is All You Need, AlexNet, OpenAI, GPT-1, GPT-2, GPT-3); CompaniesMarketCap (market cap and revenue histories of Intel, TSMC, NVIDIA, Alphabet, Microsoft); plus facts from the lineage, century, compute, labs, entry, moats and balance-a files.

Verified: 68 of 68 new facts (gate run 7 Nov 2026).
