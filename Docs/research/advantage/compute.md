# Competitive advantage: the 2026 compute stack

As of 7 November 2026. Every number cites a fact id that passed the truth gate. Market caps are from companiesmarketcap.com today and move daily.

## The shape of the stack

The AI economy runs through five narrow places. From the bottom: ASML's EUV machines, TSMC's leading-edge fabs and packaging, the three HBM makers, NVIDIA (and its challengers) at the chip layer, and the hyperscalers whose capital pays for all of it.

| Actor | Market cap, Nov 2026 | Latest quarter |
| --- | --- | --- |
| NVIDIA | $4.506 trillion [compute-nvidia-marketcap] | revenue $96.2bn, gross margin 75.0%, operating income $63.7bn [compute-nvidia-q2fy27-revenue][compute-nvidia-q2fy27-margin][compute-nvidia-q2fy27-opincome] |
| TSMC | $1.947 trillion [compute-tsmc-marketcap] | revenue $46.72bn, gross margin 66.4% [compute-tsmc-q3-revenue][compute-tsmc-q3-margin] |
| Broadcom | $1.401 trillion [compute-broadcom-marketcap] | revenue $29.6bn, AI revenue $16.7bn [compute-broadcom-q3-revenue][compute-broadcom-q3-ai] |
| Samsung | $927.27bn [compute-samsung-marketcap] | HBM share 33% [compute-samsung-hbm-share] |
| Micron | $780.68bn [compute-micron-marketcap] | revenue $54.23bn, gross margin 86.8% [compute-micron-fq4-revenue][compute-micron-fq4-margin] |
| AMD | $736.46bn [compute-amd-marketcap] | revenue $13.4bn, data center $8.5bn [compute-amd-q3-revenue][compute-amd-q3-datacenter] |
| SK Hynix | $602.37bn [compute-sk-hynix-marketcap] | revenue 104.6 trillion won, operating profit 83.33 trillion won [compute-skhynix-q3-results] |
| ASML | $549.12bn [compute-asml-marketcap] | net sales EUR 11.7bn, gross margin 57.5% [compute-asml-q3-sales] |
| Intel | $299.12bn [compute-intel-marketcap] | (not fetched) |

Two things stand out. First, NVIDIA is worth more than twice TSMC, the next largest ($4.506 trillion against $1.947 trillion), and earns an operating margin of about 66% (63.7 of 96.2). Second, the margin leaderboard has changed: SK Hynix's operating profit is about 80% of revenue (83.33 of 104.6 trillion won) and Micron's GAAP gross margin is 86.8%, above NVIDIA's 75.0%. The scarcest input now earns more than the product built from it.

## The advantages, one by one

**NVIDIA: switching costs, then scale.** Its edge is a stack, not a chip: CUDA, networking, and the libraries built on them. HPCwire still describes the market as dominated by CUDA [compute-cuda-dominates]. Analysts put NVIDIA at roughly 75% of AI accelerators in 2026, down from a peak near 87% in 2024 [compute-nvidia-accel-share]. That erosion is real but slow, and it comes from counter-positioning (below), not from AMD out-engineering it. NVIDIA's Data Center revenue was $89.0bn of $96.2bn, up 117% [compute-nvidia-q2fy27-datacenter]. It guides to $108.0bn next quarter with no China data-center compute assumed [compute-nvidia-q3fy27-guidance].

Its position is a pincer. Downstream, half its revenue is hyperscale: $48.71bn, up 102% [compute-nvidia-q2fy27-hyperscale], with $40.3bn from AI clouds, enterprises and sovereigns [compute-nvidia-q2fy27-acie]. Upstream, it has committed $279bn, up from $119bn in one quarter, "primarily related to the procurement of memory" [compute-nvidia-commitments]. It also says it has arranged help for select customers to secure land, power and data-centre capacity [compute-nvidia-customer-financing], and it invested $5bn in Intel [compute-nvidia-intel-investment]. NVIDIA is using its profit to hold up its customers and secure its inputs. A strategist reads that as defence: it is spending the moat to keep the market growing and the supply locked.

**TSMC: process power.** It holds 72.5% of top-10 foundry revenue, Samsung 5.9%, SMIC 5.4% [compute-tsmc-foundry-share][compute-samsung-foundry-share][compute-smic-foundry-share]. Advanced nodes (7nm and below) are 79% of its wafer revenue [compute-tsmc-q3-advanced-share]. It keeps 2026 capex guidance at $60-64bn [compute-tsmc-capex-2026]. The pure-play model means no customer fears it will compete with them, so each hands it the best design first.

**ASML: cornered resource.** Bloomberg calls its share of premium lithography "a cool 100%" and says it is the only EUV maker [compute-asml-euv-100]. Record quarter, EUR 11.7bn [compute-asml-q3-sales], and its own warning that supply planned for 2027 and 2028 will not fully meet demand [compute-asml-supply-short]. Everything above it in the stack is gated by its output.

**The memory makers: a cornered resource that became a profit pool.** SK Hynix says its HBM and other AI DRAM is sold out through 2029 with over half of 2027-28 capacity under long-term agreements [compute-skhynix-sold-out]. But the lead is shrinking: SK Hynix 50% of HBM in Q2 2026 against 64% a year earlier [compute-skhynix-hbm-share]; Samsung 33%, up from 21% in one quarter [compute-samsung-hbm-share]; Micron 18% [compute-micron-hbm-share]. A three-way oligopoly is turning scarcity into the margins above.

**Broadcom and Google's TPU: counter-positioning.** Broadcom's AI semiconductor revenue was $16.7bn in one quarter, up 221% [compute-broadcom-q3-ai]. It will supply about 3.5 GW of Google TPU capacity to Anthropic from 2027 and design Google's TPUs through 2031 [compute-broadcom-anthropic-tpu]. A hyperscaler that runs its own models does not need CUDA's generality, and NVIDIA cannot answer by cutting its margin without destroying the thing the market is paying for.

**AMD: the second source.** Data-center revenue was a record $8.5bn, up 97% [compute-amd-q3-datacenter], on EPYC and the MI450. Its overall gross margin is 54% [compute-amd-q3-revenue], well under NVIDIA's. Buyers back it to keep NVIDIA honest; it wins where a customer can afford a software rewrite.

**China: environment as a weapon.** Beijing told customs not to let the H200 in [compute-china-h200-customs]. NVIDIA's China shipments are under 1% of data-center revenue [compute-nvidia-china-share]. Huawei expects about $12bn of AI-chip revenue in 2026, up from $7.5bn [compute-huawei-ai-revenue]. SMIC is third in foundry [compute-smic-foundry-share] but has no EUV. China is building a separate stack, slowly, behind a wall of its own making.

## What each actor desires and fears

- **NVIDIA** wants compute to be the unit of the economy. Its fear is the buyer: the hyperscalers' capex of $803-828bn in 2026 [compute-hyperscaler-capex-2026] is the money that becomes its revenue, and the five spent $227bn in the last quarter alone [compute-hyperscaler-q3-capex].
- **TSMC and ASML** fear Taiwan and export rules, and inflation in tools.
- **The memory makers** fear the end of the shortage. SK Hynix says supply falls short of demand through 2030 [compute-skhynix-q3-results], which is the best case for them.
- **Hyperscalers** fear under-building more than over-building, which is why they buy from NVIDIA and fund their own chips at once.

## Flashpoints, 2024-2026

1. **CUDA against the second sources** (2026): NVIDIA holds, but its share falls as Broadcom and AMD grow [fp-cuda-vs-rocm].
2. **The bottleneck moves to memory** (2026): SK Hynix and Micron earn more than NVIDIA per dollar of sales [fp-memory-bottleneck].
3. **Samsung's HBM return** (Q2 2026) [fp-hbm-samsung-return].
4. **TSMC takes nearly three quarters of foundry** [fp-tsmc-foundry].
5. **EUV: one machine, one maker** [fp-euv-monopoly].
6. **The TPU bypass** [fp-custom-silicon].
7. **China shuts its own door** [fp-china-export].

## The choke points, ranked by how hard they are to replace

1. **EUV (ASML)**: no alternative; hardest to replace and slowest to expand.
2. **Leading-edge foundry and packaging (TSMC)**: 72.5% share; Samsung and Intel are years behind in trust, if not in capacity.
3. **HBM (three firms)**: sold out for years; share is moving but supply is the limit.
4. **CUDA (NVIDIA)**: the only choke point that can be eroded with software, which is why it is the one rivals attack.
5. **Hyperscaler demand**: not a supplier choke point but a buyer one: half of NVIDIA's revenue.

Flows: hyperscalers to NVIDIA about $195bn a year at the current quarterly rate (48.71 x 4); AI clouds, labs, enterprises and sovereigns about $161bn; NVIDIA to memory makers and TSMC are large but undisclosed in amount.

## What a strategist would predict

*Baseline (friendly: capex keeps rising, Taiwan quiet, export rules stable).* NVIDIA keeps winning in absolute terms while its share drifts down; ASML, TSMC and the memory makers gain most per dollar of capex, because supply is short. The profit pool tilts to whoever is scarcest: ASML and memory now, packaging next. Intel and Samsung foundry stay marginal.

*Capex pause (unfriendly: hyperscaler spending flattens).* Memory makers fall first and furthest: they run at about 80% margins on contracts and multi-year capacity, and shortage cycles end in gluts. NVIDIA's $279bn of commitments becomes a liability: it has paid for inputs that its customers no longer need, and it has financed customers whose balance sheets, in its own words, do not match their growth. Broadcom and Google fall less because they have a captive workload.

*Taiwan shock.* TSMC and NVIDIA collapse together, and ASML's orders for non-Taiwanese fabs jump. Intel and Samsung re-rate upward. NVIDIA's investment in Intel is its hedge.

*Cheaper model (a DeepSeek-style efficiency gain).* The short-term effect is on demand per task, but history (Jevons) says total compute demand rises; the winners are whoever sells inference cheapest: custom silicon and AMD gain relative to NVIDIA.

*China opens or closes further.* Opening helps NVIDIA's revenue (guidance now assumes zero) but kills Huawei's protected market; closing entrenches a second stack.

The most robust call: **ASML and the memory makers are the highest-altitude positions in the next two years because they are the hardest to replicate and are now supply-limited; NVIDIA is the largest but the most exposed to its own customers.**

## What could not be verified

See `compute.json` notes. Main gaps: NVIDIA's customer-concentration percentages (sec.gov blocks scripts), CoWoS capacity and allocation, the split of NVIDIA's commitments by supplier, Intel's latest quarter, and AMD's OpenAI contract.

## Sources

NVIDIA press release and CFO commentary for Q2 FY2027; TSMC press release for Q3 2026; AMD newsroom Q3 2026; Broadcom Q3 FY2026 release; ASML Q3 2026 release; Korea Times (SK Hynix); Pulse 2.0 (Micron); TrendForce (foundry shares, TSMC capex); Counterpoint and Edaily (HBM shares); Silicon Analysts (accelerator shares); Gizmodo/Bloomberg (ASML); Tom's Hardware (Huawei, H200 customs, Broadcom-Anthropic); Hargreaves Lansdown/Business Insider (hyperscaler capex); TechRepublic (NVIDIA-Intel); HPCwire (CUDA); companiesmarketcap.com (market caps).

Verified: 48 of 48 facts passed the gate (`python3 -I Tools/advantage/verify.py --cluster compute`).
