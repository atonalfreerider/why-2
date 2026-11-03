# Global economy view: data brief

*3 Nov 2026. For the 3D "global economy" view: the US wave of hills set among the economies that trade and compete with it, inside one world economy. Built from five verified research passes (size and structure; trade; conflict; finance; chokepoints) and their cross-checks. Raw files: `/home/john/Desktop/Unity/.why-2-build/research/global/`. Every number carries its year and source. "Computed" marks arithmetic done here on the researched numbers. US$ bn unless stated.*

## At a glance

- **Nodes.** There are 13: USA, CHN, EU27 (with Germany, DEU, drawn as a region inside it and never added to it), JPN, IND, GBR, RUS, CAN, MEX, KOR, TWN, SAU, BRA. The rest of the world (ROW) forms a horizon ring and the world (WLD) is the frame.
- **Weight (2024, computed).** The 13 hold 82.2% of world GDP and 74.7% of world GDP at PPP (IMF WEO Oct 2026), 55.1% of world population (WDI), 84.4% of military spending (SIPRI), and 66.2% of world goods exports outside intra-EU trade.
- **Arcs.** The 156 directed goods flows among the 13 carried 8,844.1 in 2024, or 43.6% of world goods exports outside intra-EU trade. The top 10 flows carry 47.8% of that value. The largest: EU27→USA 579.7, CHN→USA 525.2, CHN→EU27 516.6, MEX→USA 512.7, CAN→USA 435.1.
- **Conflict now.**
  - The Supreme Court ended the IEEPA tariffs on 20 Feb 2026.
  - The US now taxes imports through three tools: Section 301 (10-12.5% by partner, since 24 Jul 2026), Section 232 sector duties, and Section 338 against Canada (50% on C$27.6 bn, since 22 Aug 2026).
  - On PIIE's averages (22 Aug 2026), the US charges 35.2% on Chinese goods and 16.3% on everyone else; China charges 31.9% on US goods.
  - Russia remains under sanctions. The Iran war that began on 28 Feb 2026 has kept the Strait of Hormuz effectively closed.
- **Years.** 2024 is the base year, because it is the last year with every measure for every economy. 2025 is an overlay (the tariff shock). 2026 appears as dated events.

## 1. The economies

### 1a. Size (2024)

| Economy | GDP (WDI) | GDP (IMF WEO Oct 2026) | % world GDP | GDP PPP (WEO) | % world PPP | Pop., m | Labour force, m | Govt spending, % GDP |
|---|--:|--:|--:|--:|--:|--:|--:|--:|
| USA | 29,298.0 | 29,381.0 | 26.3 | 29,381.0 | 14.8 | 340.0 | 174.2 | 37.6 |
| CHN | 18,729.7 | 18,929.0 | 16.9 | 38,289.7 | 19.3 | 1,409.0 | 768.2 | 33.0 |
| EU27 | 19,501.1 | 19,592.3 | 17.5 | 28,344.8 | 14.3 | 450.2 | 221.9 | 48.9 |
| DEU (in EU27) | 4,685.6 | 4,746.9 | 4.2 | 6,080.8 | 3.1 | 83.5 | 43.8 | 49.0 |
| JPN | 4,190.0 | 4,207.6 | 3.8 | 6,774.0 | 3.4 | 124.0 | 69.5 | 37.2 |
| IND | 3,760.8 | 3,759.8 | 3.4 | 15,680.1 | 7.9 | 1,450.9 | 608.5 | 29.3 |
| GBR | 3,695.5 | 3,713.6 | 3.3 | 4,398.8 | 2.2 | 69.3 | 35.3 | 43.4 |
| RUS | 2,186.5 | 2,185.8 | 2.0 | 6,979.2 | 3.5 | 143.7 | 73.5 | 36.7 |
| CAN | 2,270.1 | 2,270.1 | 2.0 | 2,668.5 | 1.3 | 41.3 | 22.7 | 44.3 |
| MEX | 1,830.5 | 1,834.6 | 1.6 | 3,321.1 | 1.7 | 130.9 | 60.9 | 30.7 |
| KOR | 1,875.4 | 1,880.8 | 1.7 | 3,270.6 | 1.6 | 51.8 | 29.8 | 34.6 |
| TWN | — | 801.5 | 0.7 | 1,884.0 | 0.9 | 23.4 | 12.0 | 15.8 |
| SAU | 1,254.1 | 1,254.1 | 1.1 | 2,543.2 | 1.3 | 35.3 | 17.5 | 29.2 |
| BRA | 2,185.8 | 2,185.8 | 2.0 | 4,749.8 | 2.4 | 212.0 | 107.9 | 45.7 |
| 13 economies | — | 91,996.1 | 82.2 | 148,284.7 | 74.7 | 4,481.6 | 2,201.8 | |
| World | 111,669.4 | 111,906.4 | 100 | 198,532.5 | 100 | 8,140.9 | 3,697.2 | |

**Sources.**
- GDP: WDI `NY.GDP.MKTP.CD` (lastupdated 2026-10-08) and IMF WEO `NGDPD`/`PPPGDP`.
- Population: WDI `SP.POP.TOTL`; Taiwan from IMF `LP`.
- Labour force: the ILO modelled estimate in WDI; Taiwan from DGBAS (civilian).
- Government spending: IMF Fiscal Monitor Oct 2026, `G_X_G01_GDP_PT`; EU27 from Eurostat `gov_10a_main`.

**Notes.**
- **Use WEO for size.** It is one source for all 14 economies and includes the revisions that WDI is missing: BEA's 30 Sep 2026 update for the US (29,381.0, against 29,298.0 in WDI), Destatis for Germany (+1.3%), and the UK and EU27.
- **China.** WDI converts at the market rate (7.1975 yuan per US$); the IMF uses about 7.122, which gives +1.05%. Never mix the two sources for China.
- **PPP.** WDI's PPP values differ from WEO's by up to 1.6% (Germany 6,176.6, EU27 28,728.2, India 15,552.0), so take all PPP from WEO.
- **US labour force.** The BLS survey gives 168.1 m, 3.6% below the ILO model.
- **2025 is out.** WEO gives US 30,861.3 and world 118,485.3 for 2025.

### 1b. Structure (% of GDP, 2024)

| | Agri | Industry | of which mfg | Services | Exports | Imports |
|---|--:|--:|--:|--:|--:|--:|
| USA ¹ | 0.94 | 17.3 | 9.9 | 78.3 | 11.0 | 14.0 |
| CHN | 6.80 | 36.4 | 24.8 | 56.8 | 20.4 | 17.5 |
| EU27 ² | 1.62 | 22.3 | 14.3 | 66.0 | 23.1 | 20.1 |
| DEU | 0.91 | 25.6 | 18.0 | 64.0 | 41.4 | 37.7 |
| JPN | 0.96 | 26.8 | 18.8 | 71.4 | 22.0 | 22.9 |
| IND | 17.57 | 25.2 | 13.1 | 48.0 | 22.1 | 23.9 |
| GBR | 0.64 | 17.1 | 8.0 | 72.5 | 31.1 | 31.8 |
| RUS | 2.91 | 31.4 | 13.3 | 57.7 | 21.9 | 17.6 |
| CAN (VA 2022) | 1.93 | 27.4 | 9.3 | 64.1 | 32.5 | 32.7 |
| MEX | 3.87 | 31.0 | 20.1 | 58.6 | 37.3 | 38.1 |
| KOR | 1.46 | 33.9 | 26.6 | 57.5 | 44.4 | 40.3 |
| TWN ³ | 1.45 | 40.3 | 35.6 | 54.9 | 62.9 | 50.0 |
| SAU | 2.51 | 45.2 | 16.1 | 46.9 | 29.4 | 27.0 |
| BRA | 5.75 | 20.9 | 12.1 | 59.2 | 17.9 | 17.6 |
| World | | | | | 29.1 | 28.1 |

The shares are WDI value added at basic prices over GDP at market prices, so rows do not sum to 100.

1. **US:** computed from OECD table T0101, which still holds the BEA vintage from before September 2026. On BEA's own basis the shares are 0.84 / 17.30 / 9.81 / 83.48.
2. **EU27:** exports and imports are extra-EU (Eurostat `bop_eu6_q`: EUR 4,182.2 bn and 3,648.1 bn). WDI's figures including intra-EU trade are 50.4 and 46.1.
3. **Taiwan:** from DGBAS; services are sections G-S.

**Tiers.**
- Raw: agriculture. Mining sits inside industry, and only Taiwan's mining share is known (0.05% of GDP).
- Make: manufacturing. Industry minus manufacturing gives mining, utilities and construction.
- Services: the remaining tier above. The summits come from §5.

### 1c. Resources and military

| | Rents % GDP (2021) | Oil rents (2021) | Net energy imports % (2024) | Crude output % world (2024) | LNG exports % world (2024) | Military US$ bn (2024) | Military % GDP | % world military |
|---|--:|--:|--:|--:|--:|--:|--:|--:|
| USA | 1.28 | 0.61 | -9.8 | 16.19 | 21.0 | 1,004.9 | 3.43 | 37.3 |
| CHN | 1.71 | 0.31 | 24.0 | 5.17 | 0 | 311.9 | 1.66 | 11.6 |
| EU27 | 0.23 | 0.04 | 62.5 (2023) | 0.40 | 0 | 360.2 | 1.85 | 13.4 |
| DEU | 0.08 | 0.01 | 70.3 | 0.04 | 0 | 86.1 | 1.84 | 3.2 |
| JPN | 0.05 | 0.00 | 87.3 | 0.00 | 0 | 54.2 | 1.29 | 2.0 |
| IND | 3.16 | 0.33 | 36.0 | 0.72 | 0 | 85.6 | 2.22 | 3.2 |
| GBR | 0.59 | 0.42 | 47.6 | 0.72 | 0 | 89.8 | 2.44 | 3.3 |
| RUS | 18.51 | 9.67 | -70.7 | 12.07 | 8.3 | 149.4 | 6.87 | 5.5 |
| CAN | 4.95 | 2.83 | -95.0 | 5.83 | 0 | 30.6 | 1.35 | 1.1 |
| MEX | 3.64 | 2.07 | 27.4 | 2.25 | 0.1 | 20.6 | 1.13 | 0.8 |
| KOR | 0.05 | 0.03 | 83.2 | 0.00 | 0 | 47.6 | 2.54 | 1.8 |
| TWN | — | — | 95.1 † | 0.00 | 0 | 15.2 | 1.91 | 0.6 |
| SAU | 25.57 | 23.69 | -151.4 | 11.24 | 0 | 80.3 | 6.41 | 3.0 |
| BRA | 7.94 | 2.60 | -10.8 | 4.09 | 0 | 21.0 | 0.96 | 0.8 |
| World | | | | 81,958 kb/d | 405.8 Mt | 2,692.3 | | 100 |

**Sources.**
- Rents: WDI. The series ends in 2021 and reflects 2021 prices, so prefer the 2024 crude and LNG shares.
- Net energy imports: WDI `EG.IMP.CONS.ZS` (negative = net exporter). Eurostat's 2024 figures use its own definition: EU27 57.3, DEU 66.8. † Taiwan's official dependence counts nuclear as imported; on the IEA convention it is about 93.
- Crude output: EIA international statistics (84,462.6 kb/d in 2025).
- LNG: GIIGNL 2025; IGU gives a world total of 411.24 Mt.
- Military: SIPRI v1.2 (identical in WDI).

**Military notes.**
- **US:** fiscal year, including SIPRI's estimates of aid to Ukraine (NATO's 2024 figure is 935.0).
- **China, Russia:** SIPRI estimates; China's is 1.33 times the official budget.
- **Saudi Arabia:** includes the interior ministry.
- **EU27:** the sum of its 27 members (EDA: EUR 343 bn).
- **Mexico:** the 2024 figure is an unexplained spike (11.85 in 2023, 13.65 in 2025).
- **World:** the WDI sum is 2,667.0. SIPRI 2025: US 954.4, China 335.5.

## 2. Trade

### 2a. Largest flows (goods, 2024, as the exporter reports them)

| # | Flow | US$ bn | Mirror (importer's record) | % world ex intra-EU | Cumulative % of arcs |
|--:|---|--:|---|--:|--:|
| 1 | EU27→USA | 579.7 | 606.7 (Census) | 2.86 | 6.6 |
| 2 | CHN→USA | 525.2 | 440.3 (Census customs; 462.6 c.i.f.) | 2.59 | 12.5 |
| 3 | CHN→EU27 | 516.6 | ≈575.2 (Eurostat) | 2.55 | 18.3 |
| 4 | MEX→USA | 512.7 | 503.1 | 2.53 | 24.1 |
| 5 | CAN→USA | 435.1 | 411.8 | 2.15 | 29.1 |
| 6 | EU27→GBR | 371.8 | | 1.83 | 33.3 |
| 7 | USA→EU27 | 370.0 | ≈365.2 | 1.82 | 37.4 |
| 8 | USA→CAN | 350.6 | 290.9 | 1.73 | 41.4 |
| 9 | USA→MEX | 334.5 | 277.1 | 1.65 | 45.2 |
| 10 | EU27→CHN | 231.0 | | 1.14 | 47.8 |
| 11 | GBR→EU27 | 222.5 | 208.6 (HMRC basis) | 1.10 | 50.3 |
| 12 | CHN→JPN | 152.1 | | 0.75 | 52.0 |
| 13 | CHN→KOR | 147.3 | | 0.73 | 53.7 |
| 14 | USA→CHN | 143.3 | 165.4 | 0.71 | 55.3 |
| 15 | JPN→USA | 141.5 | 147.1 | 0.70 | 56.9 |
| 16 | KOR→CHN | 133.0 | | 0.66 | 58.4 |
| 17 | KOR→USA | 128.4 | 131.5 | 0.63 | 59.9 |
| 18 | JPN→CHN | 124.6 | | 0.61 | 61.3 |
| 19 | CHN→IND | 120.6 | | 0.59 | 62.6 |
| 20 | CHN→RUS | 115.6 | | 0.57 | 64.0 |
| 21 | RUS→CHN | 112.1 | 129.9 | 0.55 | 65.2 |
| 22 | TWN→USA | 111.4 | 116.3 | 0.55 | 66.5 |
| 23 | TWN→CHN | 97.0 | 217.9 (+53.6 to Hong Kong) | 0.48 | 67.6 |

**Sources.** Exporter-side figures come from IMF IMTS (updated 2026-10-31), US Census pages (for US exports) and the Taiwan MOF CSV. EU27 is extra-EU (IMF code G998).

The two records differ because of valuation (f.o.b. against c.i.f.), origin against last destination (Hong Kong, Rotterdam), and timing. Draw one side per arc and label it.

### 2b. Coverage of world trade (computed)

- **World.** Goods exports in 2024 were 24,622.1 (WDI `TX.VAL.MRCH.CD.WT`, WLD). About 4,338.6 of that is intra-EU trade (IMF), which leaves 20,283.5.
- **The 13.** They export 13,424.6, or 66.2% of the world total outside intra-EU trade (72.1% if intra-EU trade is counted on both sides).
- **Arcs.** The arcs among them carry 8,844.1: 43.6% outside intra-EU trade, 35.9% of the WDI total, and 53.5% with intra-EU trade added.
- **Inside the set.** 65.9% of the 13's exports go to each other, and 4,580.5 goes to ROW. Canada (93.9%) and Mexico (95.1%) export almost only inside the set; China (55.6%) and India (53.5%) send the most to ROW.
- **Concentration.** The top 20 arcs carry 64.0% of arc value. Arcs touching the US carry 47.6%, China 34.4%, EU27 35.7%.
- **Two-way pairs.** EU27-USA 949.7, MEX-USA 847.2, CAN-USA 785.7, CHN-EU27 747.6, CHN-USA 668.5, EU27-GBR 594.3.
- **Caveat.** The WDI world total may omit Taiwan, which would make every share above about 2% (relative) too high.

### 2c. Dependence (2024, computed): each economy's exports to the three biggest markets

| Exporter | To US, % of exports / % of GDP | To CHN | To EU27 |
|---|---|---|---|
| USA | — | 6.9 / 0.5 | 17.9 / 1.3 |
| CHN | 14.7 / 2.8 | — | 14.4 / 2.7 |
| EU27 | 21.1 / 3.0 | 8.4 / 1.2 | — |
| JPN | 20.0 / 3.4 | 17.6 / 3.0 | 9.3 / 1.6 |
| IND | 18.3 / 2.2 | 3.4 / 0.4 | 17.9 / 2.1 |
| GBR | 15.9 / 2.0 | 4.6 / 0.6 | 47.9 / 6.0 |
| RUS | 0.4 / 0.1 | 25.8 / 5.1 | 8.0 / 1.6 |
| CAN | 76.5 / 19.2 | 3.8 / 1.0 | 4.5 / 1.1 |
| MEX | 83.0 / 27.9 | 1.6 / 0.5 | 3.8 / 1.3 |
| KOR | 18.8 / 6.8 | 19.5 / 7.1 | 10.0 / 3.6 |
| TWN | 23.4 / 13.9 | 20.4 / 12.1 | 7.0 / 4.2 |
| SAU | 4.2 / 1.0 | 15.2 / 3.7 | 13.2 / 3.2 |
| BRA | 12.1 / 1.9 | 28.0 / 4.3 | 14.3 / 2.2 |

Taiwan is the only economy that sends more than 10% of its GDP to both the US and China. Russia sends 15.9% of its exports to India.

### 2d. Full matrix (2024, exporter-reported)

| From \ To | USA | CHN | EU27 | JPN | IND | GBR | RUS | CAN | MEX | KOR | TWN | SAU | BRA | ROW | Total |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| USA | · | 143.3 | 370.0 | 78.9 | 41.6 | 79.5 | 0.5 | 350.6 | 334.5 | 65.8 | 42.5 | 13.1 | 49.0 | 493.7 | 2,063.0 |
| CHN | 525.2 | · | 516.6 | 152.1 | 120.6 | 78.9 | 115.6 | 46.4 | 90.2 | 147.3 | 75.3 | 50.0 | 72.1 | 1,590.1 | 3,580.3 |
| EU27 | 579.7 | 231.0 | · | 72.9 | 52.8 | 371.8 | 34.6 | 52.2 | 57.5 | 60.4 | 31.0 | 40.0 | 47.4 | 1,115.5 | 2,746.9 |
| JPN | 141.5 | 124.6 | 66.0 | · | 17.2 | 11.2 | 2.2 | 11.2 | 12.3 | 46.5 | 45.3 | 6.1 | 4.6 | 218.8 | 707.5 |
| IND | 80.9 | 15.1 | 79.1 | 5.8 | · | 14.3 | 4.9 | 4.1 | 5.7 | 6.0 | 1.7 | 12.3 | 6.5 | 205.9 | 442.5 |
| GBR | 73.7 | 21.4 | 222.5 | 9.3 | 9.0 | · | 0.9 | 8.9 | 2.2 | 5.3 | 2.4 | 5.5 | 3.5 | 99.9 | 464.4 |
| RUS | 1.8 | 112.1 | 34.7 | 4.4 | 68.7 | 0.1 | · | 0.0 | 2.5 | 6.7 | 3.2 | 5.1 | 10.1 | 188.7 | 433.6 |
| CAN | 435.1 | 21.8 | 25.3 | 10.9 | 3.9 | 20.6 | 0.0 | · | 6.3 | 5.6 | 1.5 | 1.5 | 1.8 | 34.9 | 569.1 |
| MEX | 512.7 | 9.9 | 23.2 | 4.4 | 2.7 | 3.2 | 0.0 | 18.9 | · | 6.2 | 0.7 | 0.6 | 5.0 | 30.2 | 617.7 |
| KOR | 128.4 | 133.0 | 68.2 | 29.6 | 18.7 | 6.6 | 4.6 | 10.4 | 13.6 | · | 34.0 | 5.2 | 5.3 | 226.1 | 683.6 |
| TWN | 111.4 | 97.0 | 33.5 | 25.8 | 7.9 | 3.7 | 0.6 | 2.6 | 7.9 | 20.8 | · | 1.0 | 1.2 | 161.8 | 474.9 |
| SAU | 12.8 | 46.3 | 40.1 | 28.4 | 27.2 | 2.1 | 0.0 | 1.6 | 0.0 | 28.6 | 7.9 | · | 2.9 | 106.0 | 303.9 |
| BRA | 40.9 | 94.4 | 48.2 | 5.6 | 5.3 | 3.1 | 1.5 | 6.3 | 7.8 | 5.5 | 1.8 | 3.1 | · | 113.5 | 337.0 |

**How to read the matrix.**
- **Small cells and totals.** 0.0 means under 0.05 bn. ROW is computed as each row's total minus the listed partners. EU27's total is world exports minus intra-EU exports.
- **Russia.** Russia's cells are IMF figures built from partner records; Russia has published no partner data since 2022. Its totals come from the Bank of Russia's balance of payments (imports 301.5, against IMF's 239.9).
- **UK.** The UK row excludes non-monetary gold. On HMRC's basis, which includes gold, UK exports are 520.2 (65.4 of it gold).

**Germany inside the EU27 (exports to / imports from):**
- USA 174.5 / 75.7; CHN 97.3 / 107.1; GBR 86.8 / 40.9.
- Intra-EU: 907.4 to the other 26 members and 971.4 from them.

EU27→DEU (971.4) is the largest cell in the research. It is intra-EU trade on Eurostat's community concept, which includes quasi-transit (NL→DE 216.3); the national concept gives 856.9. Draw it inside the EU landscape, not as an arc.

### 2e. US services by partner (BEA, 2024; exports / imports)

| Partner | Exports / imports |
|---|---|
| EU27 | 296.9 / 200.0 |
| GBR | 100.1 / 91.5 |
| CAN | 90.5 / 61.5 |
| CHN | 54.5 / 22.3 |
| MEX | 51.1 / 40.6 |
| JPN | 49.4 / 39.8 |
| DEU | 45.0 / 44.9 |
| IND | 41.8 / 43.1 |
| BRA | 29.7 / 6.3 |
| KOR | 27.5 / 14.6 |
| TWN | 13.7 / 13.0 |
| SAU | 11.4 / 2.1 |
| RUS | 1.2 / 0.4 (via USTR) |
| **Total** | **1,156.7 / 829.4** |

Services trade between non-US pairs was not researched.

### 2f. 2025 overlay (US imports from each partner, Census; 2024 → 2025)

| Partner | 2024 → 2025 | Change |
|---|---|--:|
| EU27 | 606.7 → 632.9 | |
| MEX | 503.1 → 534.3 | |
| CAN | 411.8 → 381.9 | |
| CHN | 440.3 → 308.7 | −29.9% |
| TWN | 116.3 → 201.4 | +73.2% |
| JPN | 147.1 → 145.8 | |
| KOR | 131.5 → 125.5 | |
| IND | 87.3 → 103.8 | |
| GBR | 68.2 → 64.8 | |
| BRA | 42.3 → 39.9 | |
| SAU | 12.9 → 10.5 | |
| RUS | 3.0 → 3.8 | |

**Exporter-side 2025 values:** CHN→USA 421.0, TWN→USA 198.3, MEX→USA 552.0, EU27→USA 623.6, CAN→USA 403.9.

The gap between China's and the US's records of the same flow widened from 84.9 to 112.4. Label a 2024 matrix "before the 2025 tariff shock".

## 3. Conflict and coercion

### 3a. Tariff walls on 3 Nov 2026

| Partner | US tariff now | Partner's response |
|---|---|---|
| China | 2018-24 Section 301 lists (25% / 7.5%; EVs 100%) plus the 12.5% forced-labour rate (24 Jul 2026) plus Section 232. PIIE average 35.2%, covering 89% of imports | 31.9% average; mineral and rare-earth controls (the Oct 2025 expansion is suspended to 10 Nov 2026) |
| Canada | 10% (Section 301), plus Section 338: 50% on C$27.6 bn (22 Aug 2026) and import bans from 29 Sep 2026. The USMCA was not renewed on 1 Jul 2026 | 15/25/50% on C$27.6 bn (8 Sep 2026) |
| Mexico | 10% | Up to 50% on about 1,463 tariff lines from countries without a trade deal (1 Jan 2026; secondary source) |
| EU27, Taiwan | 10% all-in | — |
| Japan, Korea | 12.5% all-in | — |
| India | 10% (the Feb 2026 deal set 18% and ended the Russian-oil penalty) | — |
| UK | 10% | — |
| Brazil | 12.5% plus 25% Section 301 on certain goods (22 Jul 2026) | — |
| Saudi Arabia, Russia | 12.5%. For Russia, the Graham Act's 500% is set but waived (16 Oct 2026) | — |
| All partners (Section 232) | Steel and aluminium 50% (UK 25%); autos 25% (EU, Japan, Korea 15%); copper 50%; pharmaceuticals 100% (15% for deal partners); a narrow class of advanced chips 25% (15 Jan 2026) | |

**Sources:** 91 FR 47717; Proclamations 10947, 10908 and 11002; Finance Canada; USTR; EO 14436; PIIE.

### 3b. US-China average tariffs (PIIE workbook, 22 Aug 2026; %)

| Date | US on CHN | CHN on US | Event |
|---|--:|--:|---|
| Jan 2018 | 3.1 | 8.0 | before the trade war |
| 14 Feb 2020 | 19.3 (on 66.6% of imports, about US$335 bn) | 21.3 (on 58.3%) | Section 301 rounds 1-4A; Phase One (China's US$200 bn purchase pledge went unmet) |
| 1 Jan 2025 | 20.7 | | 2024 Section 301 increases |
| 12 Apr 2025 | | 147.6 | peak |
| 3 May 2025 | 126.1 | | peak; the statutory IEEPA rate was 145% (fentanyl 20% plus reciprocal 125%) |
| 14 May 2025 | 51.7 | 32.6 | Geneva truce (combined IEEPA rate 30%) |
| 10 Nov 2025 | 47.4 | 31.9 | fentanyl duty cut from 20% to 10% |
| 20 Feb 2026 | 31.2 | | IEEPA tariffs ended (47.6 just before) |
| 24 Feb 2026 | 36.8 | | 10% Section 122 surcharge, to 24 Jul 2026 |
| 22 Aug 2026 | 35.2 | 31.9 | Section 301 forced-labour regime |

These are statutory rates weighted by fixed 2017 trade, not duties actually collected.

**Federal Register dates:**
- 25% on US$34 bn: 6 Jul 2018.
- 25% on US$16 bn: 23 Aug 2018.
- 10% on US$200 bn: 24 Sep 2018, raised to 25% on 10 May 2019.
- List 4A: 15% on 1 Sep 2019, cut to 7.5% on 14 Feb 2020.

**US average on the rest of the world (PIIE):** 3.0% (20 Jan 2025), 18.9% just before the ruling, 12.3% (20 Feb 2026), 16.3% (22 Aug 2026).

### 3c. Events

| Event | Parties | Dates | Magnitude | Source |
|---|---|---|---|---|
| Sanctions on Russia | USA, EU27, GBR, JPN, KOR, CAN vs RUS | from 24 Feb 2022 | US imports from Russia 29.6 (2021) → 3.0 (2024). EU imports EUR 163.6 bn → 36.3 bn (2024) → 27.9 bn (2025). India's imports from Russia 8.7 → 65.7; China's 78.4 → 128.3 (2021 → 2024) | Census; Eurostat; IMF |
| Gas cut | RUS vs EU27 | from Jun 2022 | Russia's share of EU gas imports 45% (2021) → 19% (2024) → 12% (2025); Germany's 65.4% → 0 | European Commission IP/25/1131; Eurostat |
| Reserves frozen | G7, EU vs RUS | from 28 Feb 2022 | About US$300 bn (EPRS 2024); Euroclear holds EUR 202 bn (Jun 2026); G7 loans of US$50 bn; EU loan of EUR 90 bn (2026-27) | Treasury; Euroclear |
| Oil price cap | G7, EU vs RUS | from 5 Dec 2022 | US$60 → 47.60 → 44.10 (Feb 2026), frozen to Jul 2027. Europe's share of Russia's crude exports 51% (2020) → 12% (2024) | DG FISMA; EIA |
| India penalty | USA vs IND | 27 Aug 2025 – 7 Feb 2026 | +25% over Russian oil (50% in total) | EO 14329, EO 14384 |
| Iran sanctions | USA (EU27 and GBR via snapback) vs IRN | from 8 May 2018 | 700+ designations; EU imports from Iran EUR 10.1 bn (2017) → 0.85 bn (2024) | Treasury; Eurostat |
| Chip export controls | USA vs CHN | from 7 Oct 2022 | US exports to China of chip-making machines (HS 8486): 6.81 (2021) → 2.28 (2025). Chips (HS 8542): 12.27 → 9.96. 140 entities added to the Entity List (Dec 2024) | BIS; Comtrade |
| China's mineral controls | CHN vs all | from 1 Aug 2023; ban on the US suspended to 27 Nov 2026 | China's germanium-metal exports, Jan-Sep: 36,656 kg (2023) → 7,520 kg (2025). US germanium imports −67% (2025). Antimony price US$9.8 → 18.1 per lb (2024) | USGS MCS 2026 |
| China's rare-earth controls | CHN vs all | 4 Apr 2025; the Oct 2025 expansion suspended to 10 Nov 2026 | China mines 71% and refines 91% of magnet rare earths | USGS; IEA |
| China vs Japan, dual-use | CHN vs JPN | from 6 Jan 2026 | Military end users banned; 20 entities listed | MOFCOM |
| Japan vs Korea | JPN vs KOR | 2019-2023 | Japan's hydrogen-fluoride exports to Korea US$67.9 m → 12.0 m | Comtrade |
| DRC cobalt | COD vs all | from 22 Feb 2025 | Export quota of 96,600 t a year (2026-27), against 226,000 t mined in 2024 | USGS |
| Seafood ban | CHN vs JPN | 2023-2025 | China's imports US$506.8 m → 0.11 m | Comtrade |
| Pressure on Taiwan | CHN vs TWN | from 2024 | ECFA tariff cuts withdrawn on 12, then 134 tariff lines; 153 PLA aircraft in one day (Oct 2024; secondary source) | Tariff Commission; Taipei Times |
| EV duties | EU27 vs CHN | from 30 Oct 2024 | EU duties 7.8-35.3%; China's pork duties 4.9-19.8% | European Commission; MOFCOM |
| Canola | CAN vs CHN | 2024-2026 | China's rapeseed imports from Canada 3.29 → 1.29 | Comtrade |
| US vs Canada, 2025 | USA vs CAN | 4 Mar 2025 – 20 Feb 2026 | US 25% (35% from 1 Aug); Canada retaliated on C$30 bn, then C$29.8 bn more | EO 14193 |
| Ukraine war | RUS vs UKR | from 24 Feb 2022 | 17,605 civilians killed and 55,846 injured (to 30 Sep 2026) | OHCHR |
| Iran war, Hormuz | USA (with Israel) vs IRN | from 28 Feb 2026 | Brent US$71.32 → 94.35 (9 Mar); high 138.21 (7 Apr); September average 114.16. Gulf oil shut-ins 10.9 mb/d at the May peak, 4.8 in September | EIA |
| Galwan clash | IND vs CHN | 15 Jun 2020 | 20 Indian soldiers killed | secondary source |
| Saudi-Russia price war | SAU vs RUS | Mar-Apr 2020 | Brent US$55.66 → 18.38 | EIA |

**Blocs:**
- **NATO** (core spending, 2025 estimates, 2026 edition): US 3.17%, UK 2.32%, Canada 2.03%, Germany 2.22%, Europe and Canada together 2.31%. The pledge is 5% by 2035.
- **EU single market:** 60.9% of members' exports stay inside the EU (2024).
- **USMCA:** 17.8% of world GDP at PPP.
- **Quad:** 27.1% at PPP.
- **BRICS:** 38.7% at PPP (40.0% with Saudi Arabia) (IMF 2024).
- **China-Russia trade:** 146.8 (2021) → 245.2 (2024).
- **OPEC:** the UAE left on 1 May 2026, cutting OPEC's share of world crude from 35% to 31%.

**Still open:** the rare-earth suspension and the US-China truce terms expire on 10 Nov 2026.

## 4. Finance

| | US Treasuries held, Aug 2026 (Aug 2025) | All US long-term securities, Aug 2026 | Current account 2024, US$ bn / % GDP | Net IIP end-2024, US$ bn | Net IIP % GDP (corrected) | US FDI in partner / partner FDI in US, end-2024 |
|---|---|--:|---|--:|---|---|
| USA | — | — | -1,198.6 / -4.1 | -22,127.9 | -75.3 | — |
| CHN | 590.7 (699.7) | 1,106.8 | 458.5 / 2.4 | 3,177.7 | 16.8 | 112.0 / 32.4 |
| EU27 | 2,144.6 (2,102.1) | 11,439.8 | 486.7 / 2.48 (extra-EU) | 1,177.2 (excl. reserves) | 6.25 | 2,660.0 / 2,418.2 |
| DEU | 96.5 (100.6) | 730.7 | 276.0 / 5.8 | 3,583.8 | 78.7 (reported 75.5) | 216.1 / 488.0 |
| JPN | 1,068.3 (1,183.9) | 3,055.5 | 193.0 / 4.6 | 3,435.1 | 84.5 (81.6) | 63.9 / 749.6 |
| IND | 219.7 (219.4) | 235.0 | -22.9 / -0.6 (fiscal year) | -367.4 | -9.8 | 63.6 / 5.1 |
| GBR | 1,009.5 (901.3) | 4,232.4 | -107.7 / -2.9 | -51.4 | -1.4 | 1,004.8 / 718.1 |
| RUS | 0.025 (0.039) | 2.7 | 63.3 / 2.9 | 949.6 | 47.7 (43.4) | 6.5 / not shown |
| CAN | 464.6 (444.0) | 3,489.3 | -10.9 / -0.5 | 1,401.8 | 64.8 (61.8) | 450.6 / 708.2 |
| MEX | 103.5 (91.7) | 265.6 | -16.8 / -0.9 | -571.1 | -34.5 (-31.1) | 155.9 / 41.9 |
| KOR | 134.2 (135.8) | 1,009.9 | 100.0 / 5.3 | 1,086.4 | 62.3 (57.8) | 35.0 / 90.4 |
| TWN | 298.4 (313.4) | 902.3 | 114.6 / 14.3 | 1,512.3 | 188.7 (not rechecked) | 19.6 / 15.5 |
| SAU | 141.2 (133.0) | 343.5 | -16.3 / -1.3 | 783.1 | 62.4 | 9.7 / 14.6 |
| BRA | 168.4 (184.6) | 196.8 | -65.3 / -3.0 | -835.2 | -43.9 (-38.2) | 85.2 / 6.7 |
| World | 9,331.7 (9,249.1); official holders 3,755.6 (3,919.8) | 39,916.3 | | | | 6,697.7 / 5,598.5 |

**Sources.**
- Holdings: TIC Tables 5, 3 and 1 (16 Oct 2026). TIC records the custodian's country (Belgium 490.5, Luxembourg 448.2, Cayman 463.0, Ireland 352.1), so China's holdings may be understated.
- Current account: IMF WEO; China's own agency (SAFE) reports 462.3; EU27 from Eurostat.
- 2025 current accounts: USA -1,177.1, CHN 724.4, TWN 184.4 (20.0% of GDP), KOR 123.1.
- Net IIP: IMF IIP, CBC (Taiwan) and Eurostat (EU27).
- Direct investment: BEA (21 Jul 2026). Korea's inbound figure comes from BEA's ultimate-owner table.

**Net IIP and reserves notes.**
- **Corrected ratios:** the reported figures divided year-end stocks by GDP at average exchange rates. The cross-check recomputed seven economies in national currency (Eurostat publishes 78.6 for Germany).
- **US revision:** BEA's update of 24 Jun 2026 moved end-2024 from −26.54 to −22.13 trn. The latest figure is −22.42 trn (end-June 2026).
- **Korea:** Q2-2026 is 64.0, down from 753.6, which is unverified; do not use it.
- **Dollar's share of FX reserves (COFER):** 58.41% (2024-Q4) and 56.70% (2026-Q2). The IMF changed its method (it now imputes unreported reserves) from the 2025-Q3 release.

**Pledges:**
- **Japan:** US$550 bn, invested where the US directs, with 90% of profits to the US. The reported 50/50 split until recoupment is unverified.
- **EU27:** US$600 bn of investment plus US$750 bn of energy purchases through 2028 (intentions).
- **Korea:** US$350 bn (an MOU caps funding at US$20 bn a year).
- **Taiwan:** US$250 bn.

## 5. Chokepoints

| Keystone | Year | Holder (% of world) | Source |
|---|---|---|---|
| Logic below 10 nm | 2022 | TWN 69, KOR 31, USA 0 (2032 forecast: TWN 47, USA 28) | SIA/BCG 2024 |
| All wafer capacity | 2022 | CHN 24, TWN 18, KOR 17, JPN 17, USA 10, Europe 8 | SIA/BCG |
| Rare-earth mining | 2024 | CHN 71.1, USA 12.0, AUS 7.6, MMR 7.1 | USGS MCS 2026 |
| Magnet rare-earth refining | 2024 | CHN 91 | IEA GCMO 2025 |
| Gallium (primary) | 2024 | CHN 98.9 | USGS |
| Germanium refining | 2016-20 | CHN 83.1, EU 7.2, RUS 5.0 | EU CRM 2023 |
| Graphite | 2024-25 | CHN 82 of mining (USGS's tonnages imply 77.8); 95.7 of battery-grade refining | USGS; IEA |
| Lithium mining | 2024 | AUS 37.3, CHL 22.0, CHN 18.6 (IEA: 22.4) | USGS |
| Lithium refining | 2024 | CHN 70, USA 1 | IEA |
| Cobalt mining | 2024 | COD 74.8 (IEA: 66.7) | USGS |
| Cobalt refining | 2024 | CHN 78 | IEA |
| Crude oil production | 2024 | USA 16.2, RUS 12.1, SAU 11.2 | EIA |
| Crude oil exports (value) | 2024 | SAU 14.1, RUS ≥9.4, USA 9.0, ARE 8.6, CAN 8.5 | Comtrade |
| LNG exports | 2024 | USA 21.0, AUS 19.5, QAT 19.3, RUS 8.3 | GIIGNL |
| Data-centre electricity | 2024 | USA 45, CHN 25, Europe 15 (world total 415 TWh) | IEA |
| AI compute (H100-equivalents) | end-2024 | USA 76.95, CHN 9.12, EU27 4.63 | Epoch |

**Straits and pipelines:**
- **Hormuz:** 20 mb/d of oil in 2024 (about 20% of world consumption) and about a fifth of world LNG. Effectively closed since 28 Feb 2026; Qatar's LNG (over 10 Bcf/d) has been under force majeure since 4 Mar 2026. An MOU on 17 Jun 2026 was followed by renewed strikes in July.
- **Saudi East-West pipeline:** carried more than 5.0 mb/d before it was attacked in September 2026.
- **Bab el-Mandeb:** oil flows fell from 8.7 mb/d (2023) to 4.0 (Jan-Aug 2024).
- **Suez Canal:** tonnage −70% by mid-2024.
- **Sources:** EIA; UNCTAD.

## 6. Solid, disputed, missing

**Solid** (confirmed by a second source):
- **Military:** exact match with SIPRI.
- **China:** GDP within 0.08% of the OECD figure.
- **Euro-area and IMF series:** extra-EU trade, government spending, TIC, COFER and the US net IIP all match on re-fetch.
- **Trade:** CHN→USA, EU27→USA, CHN→EU27 and CAN→USA all agree within 0.2%. Census imports agree with Comtrade within 0.3%.
- **Tariffs:** Federal Register texts are verbatim, and the PIIE workbook reproduces.
- **Chokepoints:** USGS, IEA, EIA and GIIGNL shares; GIIGNL is within 1.3% of IGU.
- **Blocs:** shares within 0.4 pp between the IMF and the World Bank.

**Disputed** (research value → cross-check → what to use):
- **GDP vintages in WDI** (US, DEU, GBR, EU27): stale → use WEO.
- **China's GDP in US$:** WDI vs IMF exchange-rate convention → use one source only.
- **US labour force:** 174.2 m (ILO) vs 168.1 m (BLS) → use the ILO figure, labelled as a model.
- **US value-added basis:** services 78.27 vs 83.48 → use the OECD basis, labelled.
- **US military:** SIPRI 1,004.9 vs NATO 935.0 → use SIPRI, with the note.
- **Mexico military 2024:** unexplained spike → flag it.
- **CHN↔USA trade:** 525.2 vs 440.3 and 143.3 vs 165.4 → draw one side, show the other in the label.
- **TWN→CHN:** 97.0 vs 217.9 recorded by China → draw a Hong Kong stub.
- **Russia's cells:** 112.1 vs 129.9 → approximate; draw them dashed.
- **UK cells:** IMF vs HMRC (gold, plus 3.5 unexplained) → use IMF, labelled.
- **EU27→DEU:** 971.4 vs 856.9 (national concept) → show inside the EU only.
- **Mexico's export share to the US:** 83.0 vs 81.2 → show the range.
- **Section 301 history:** the research gave wrong tranche dates → use §3b.
- **IEEPA on China:** the peak was 145% and the post-truce rate 30%, not 10% → use §3b.
- **Brent on 9 Mar 2026:** 104 → 94.35.
- **Oil-shock peak:** 131 → 138.21 (7 Apr).
- **Russia's share of EU gas:** 40.9 / 11.4 → 45 / 19 (Commission).
- **Ukraine civilian deaths:** 16,874 → 17,605.
- **Euroclear:** EUR 200 bn → 202 bn.
- **NATO:** 2025 edition → 2026 edition.
- **Graphite:** 82% vs 77.8% implied → quote 82 as the stated share.
- **China's current account:** WEO vs SAFE (462.3 / 735.0).
- **Net IIP % of GDP:** recompute in national currency.
- **COFER:** don't mix pre- and post-change bases.
- **Russia's frozen reserves:** missing → US$300 bn (EPRS).
- **US-Japan profit split:** flag as unverified.
- **CHIPS Act tax credit:** 25% → 35%.
- **Nov 2025 US-China deal:** its tariff side is void after the IEEPA ruling.
- **Refinery runs:** −4.8 mb/d (attributed to EIA) → IEA −4.5.
- **EU27 crude exports:** 1.95% → at most 0.28% once intra-EU transit is removed.
- **Russia's crude exports:** a lower bound.
- **Cobalt and lithium:** USGS vs IEA → show both.
- **Rare-earth scope:** total oxide vs magnet elements → name the scope.
- **EU cobalt refining:** 17% → about 10% (2024).
- **US LNG:** 2025 exports were 26% higher.
- **Epoch de-duplication:** 76.95 vs 76.33 → state the rule.
- **OPEC shares:** name the basis.

**Missing:**
- **Resources:** rents after 2021; Taiwan's rents and its net energy imports on the WDI concept.
- **Late years:** Canada's value added after 2022; EU27 energy for 2024 on the WDI concept.
- **Russia:** its own partner trade data; its direct investment in the US.
- **Pairs not involving the US:** services, FDI and portfolio positions.
- **Frozen reserves:** the EU's share of Russia's frozen reserves (sources blocked).
- **Chokepoint data:** daily transits (PortWatch needs a token).
- **Blocked sources:** Energy Institute and OPEC data; UK ONS and Bank of Korea checks.
- **Attention:** no attention data for non-US economies.
- **AI and data-centre shares:** missing for most economies.
- **Minerals:** germanium after 2020; US lithium output.
- **Trade:** a full 2025 matrix; tariffs actually collected (the Census API needs a key).
- **Events sourced only secondhand:** PLA counts, Galwan casualties on the Chinese side, Mexico's 2026 tariffs, the Canada-China terms, and the IEA's 400 mb stock release.
- **Not a gap:** Taiwan's net IIP (CBC: 1,512.3 and 1,349.6).

## 7. Recommended dataset and recipes

**Scope.**
- **Economies:** the 13, plus Germany as a sub-region of the EU27, ROW (world minus the 13) and WLD.
- **Years:** 2024 for flows and structure; end-2024 for stocks (end-2025 as "latest"); TIC at end-Aug 2026; the 2025 overlay; 2026 events.
- **Sources:** one per measure, with the vintage stamped on each value.

The companion `dataset` JSON holds these measures as arrays in a fixed economy order, plus the trade matrix, mirror values, tariffs, events and source URLs.

| Layer | Measure | Source and year | Recipe |
|---|---|---|---|
| Footprint | GDP (PPP as a toggle) | WEO 2024 | R2 |
| People | population, labour force | WDI 2024 | R1, R6 |
| Tiers | value-added shares | WDI / OECD / DGBAS | R1, R3, R6 |
| Summit | <10 nm logic, wafers, data centres, AI compute | SIA/BCG, IEA, Epoch | R8 |
| Bedrock | government spending % GDP (the same series as `regimes.json`) | IMF Fiscal Monitor 2024 | R2, R4 |
| Armour | military spending | SIPRI 2024 | R5 |
| Ground | crude, LNG, net energy imports | EIA, GIIGNL, WDI | R7, R8, R1 |
| Arcs | goods exports (exporter side, mirror in the label); US services | IMF IMTS, Census, MOF, Bank of Russia, BEA | R4, R6, R7, R9 |
| Clouds | net IIP, current account, TIC, FDI | IMF, CBC, Eurostat, TIC, BEA | R2, R4, R9 |
| Walls and cuts | tariffs; trade before and after | PIIE, Federal Register, Census, Eurostat | R9 |
| Keystones and straits | input shares; strait flows | USGS, IEA, EIA, Comtrade, UNCTAD | R7, R8 |

**Build layout.** Follow the same pattern as `regimes.json`:
- `Tools/global/build.py`, with hand-read values in `Tools/global/sources.json` (each with its URL and a quote).
- Downloads cached in `.cache`, with an `--offline` flag to rebuild from the cache.
- Output to `Assets/Resources/Data/economy/global.json`, each value as `{value, year, source, url, note, quote}`.
- User-Agent `why-2-global/0.1 (https://github.com/atonalfreerider/why-2)`.
- Run parsers with `python3 -I`. No source needs a key.

**Recipes.**

- **R1 WDI.**
  - Call `https://api.worldbank.org/v2/country/USA;CHN;EUU;DEU;JPN;IND;GBR;RUS;CAN;MEX;KOR;SAU;BRA;WLD/indicator/{CODE}?format=json&date=2010:2025&per_page=1000`. The response is `[meta, rows]`; take 2024 or the latest year with data.
  - Codes: SP.POP.TOTL, SL.TLF.TOTL.IN, NV.AGR/IND.TOTL/IND.MANF/SRV.TOTL.ZS, NE.EXP/IMP.GNFS.ZS, EG.IMP.CONS.ZS, TX.VAL.MRCH.CD.WT.
  - Use a 150 s timeout and retry once. WDI has no Taiwan.
- **R2 IMF DataMapper.**
  - Call `https://www.imf.org/external/datamapper/api/v1/{NGDPD|PPPGDP|LP|G_X_G01_GDP_PT|BCA|BCA_NGDPD|PPPSH}`. It returns every country and year, so cache the response and read `values[IND][code][year]`.
  - Codes: TWN, EU, WEOWORLD.
  - Send the project User-Agent; some other agents get a 403.
- **R3 OECD (US value added).**
  - Call `https://sdmx.oecd.org/public/rest/data/OECD.SDD.NAD,DSD_NAMAIN10@DF_TABLE1_OUTPUT,/A.USA........V..?startPeriod=2021&format=csvfilewithlabels`.
  - Divide B1G by B1GQ for ACTIVITY A, C, BTE+F and GTI…RTU.
- **R4 Eurostat (JSON-stat).**
  - Base URL: `https://ec.europa.eu/eurostat/api/dissemination/statistics/1.0/data/`.
  - Datasets: `bop_eu6_q` (geo=EU27_2020, partner=EXT_EU27_2020, bop_item=GS/CA, s_adj=NSA), `nama_10_gdp`, `gov_10a_main`, `nrg_ind_id`, `bop_iip6_q` (FA__TXR__F), `ert_bil_eur_a`, `ext_lt_maineu`, `nrg_ti_gas`.
  - For `nrg_ti_gas`, divide Russia's volume by extra-EU imports only.
- **R5 SIPRI.**
  - Download `https://www.sipri.org/sites/default/files/SIPRI-Milex-data-1949-2025_v1.2.xlsx` and read the sheets "Current US$" and "Share of GDP".
  - EU27 is the sum of the 27 members.
- **R6 Taiwan.**
  - National accounts and labour force from the DGBAS xlsx files (scrape https://eng.stat.gov.tw/cp.aspx?n=2334).
  - Energy from `https://ea01.moeaea.gov.tw/a0303/02/api/v1/zone/annually/1/1`.
  - Trade from `https://service.mof.gov.tw/public/data/statistic/trade/u2010ex.csv` and `u2010im.csv` (Big5 encoding, US$ thousand; EU27 is the sum of the 27 member columns).
  - Net IIP from the CBC tables.
- **R7 Trade.**
  - IMF IMTS: call `https://api.imf.org/external/sdmx/2.1/data/IMF.STA,IMTS/{E}.XG_FOB_USD+MG_CIF_USD.{E}+G001.A?startPeriod=2024&endPeriod=2025&detail=dataonly` with header `Accept: application/vnd.sdmx.data+csv;version=1.0.0`. G998 is the EU27; Taiwan reports nothing.
  - Census: the API needs a key, so parse `https://www.census.gov/foreign-trade/balance/c{code}.html` for the "TOTAL yyyy" rows. Codes: 5700, 0003, 4280, 5880, 5330, 4120, 4621, 1220, 2010, 5800, 5830, 5170, 3510.
  - Bank of Russia: `bal_of_payments_standart.xlsx`, rows 13-14.
  - EIA: `https://www.eia.gov/opendata/bulk/INTL.zip`, series INTL.57-1-{ISO3}-TBPD.A.
  - Comtrade preview: `https://comtradeapi.un.org/public/v1/preview/C/A/HS?...` (about 1 call every 2 s). For crude export shares, use HS 2709 import mirrors and drop intra-EU flows.
- **R8 Hand-read sources** (into `sources.json`, with page and quote):
  - Minerals: USGS MCS 2026 PDFs.
  - IEA PDFs, from the blob host.
  - EU CRM 2023.
  - SIA/BCG Exhibit 6: render page 14 and read the bar colours.
  - GIIGNL 2025.
  - NATO 2026 (the tables are images).
  - OHCHR; EIA STEO and Today in Energy; UNCTAD; Euroclear (`cf.euroclear.com`).
  - Epoch: download `https://epoch.ai/data/gpu_clusters.csv` and sum H100-equivalents by country, dropping superseded "Phase 1" rows.
- **R9 Finance and policy.**
  - TIC: `slt_table5.txt`, `slt_table3.txt` and `slt_table1.txt`, using column 4.
  - COFER: `https://api.imf.org/external/sdmx/2.1/data/IMF.STA,COFER/G001....Q?startPeriod=2024-Q1`.
  - IMF IIP: `.../IMF.STA,IIP/{codes}.NETAL_P.NIIP.USD.A+Q?startPeriod=2023`. Compute ratios to GDP in national currency.
  - BEA: the DI xlsx files (`usdia-…`, `fdius-…`; Korea from the `fdius-ubo` file) and the services geo xlsx (Tables 7-8). File names change.
  - PIIE: the tariff xlsx (sheets "Panel a" and "Panel b") and the Datawrapper `dataset.csv` behind the timeline.
  - Federal Register: `https://www.federalregister.gov/api/v1/documents/{docnum}.json` (follow `raw_text_url`).

**Checks.**
- Never sum DEU with EU27.
- For each row: partners plus ROW must equal the total.
- Flag mirror gaps above 15%.
- Stamp the vintage on every value, and never mix WDI and WEO for China.
- EU27 military spending must equal the sum of the 27 members.
- Compute net IIP ratios in national currency.
- Shares must not exceed 100%.
- Record any fallback years.
- Check event dates against the latest source.
