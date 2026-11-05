# Map: objects and images (map:objects)

What the economy can draw today, which real actors the holistic scenes would show and the identifiers we already hold for
them, and what it would take to show thumbnails of those actors: storage, drawing, attribution, pipeline and the Quest
budget. Written 4 Nov 2026 against the working tree on main (d7e2ef0 plus the uncommitted G3b files: LabelSystem.cs,
HillLabels.cs, Emphasis/). No repository file was changed.

Evidence I gathered for this map (scratch, durable): `/home/john/Desktop/Unity/.why-2-build/work/holistic/map-objects/`
- `probe.json`: one row per probed actor (QID, image property, Commons file, licence, restrictions, size). Built by
  `bin/probe.py` with `bin/wd.py` (19 cached GETs to Wikidata, its query service and Commons, the brief's User-Agent; cache
  in `cache/`).
- `proto/view.png`, `proto/atlas2048.png`: a prototype atlas page baked with ffmpeg alone (a round photo, a logo tile and a
  monogram tile in Liberation Sans).

---

## 0. In one page

1. **Every object in the economy is a line drawing.** Firms are three gold line rings on a beam, people are a stick and a
   wire-sphere head, the attention cloud's people are three crossed strokes, officeholders are a stick with a circle. The
   only images in the project are 12 procedural universe glyphs (IconRaster, one 512 px alpha atlas) and the XR menu's
   line-art icons. There is no photo, logo, flag or emoji anywhere, and the label font has no emoji (EVALUATION Q27).
2. **The objects are also the draw-call problem.** The activity layer gives each of 640 cohort markers (128 cohorts x 5) its
   own mesh, material and renderer, and each of 25 firms three more. One token mesh per layer would draw all of a layer's
   objects in one call.
3. **The label system already carries an icon slot** (`LabelSpec.Icon`, a SpriteRenderer child of each label). Today it
   resolves only the 12 universe glyphs, tints the icon with the text colour and sizes it at 1.45 em (about 16 px beside a
   13 px label). A photo needs a white tint, a larger size and a second atlas. Four small changes in LabelSystem cover it.
4. **The identifiers mostly exist.** people.json holds Wikidata QIDs for 1,003 of 1,074 accounts (354 of 359 US adults alive
   in 2025); visibility.json for the six lives candidates; global.json uses ISO 3166 alpha-3 codes; the 25 stored firms
   have tickers. Missing: QIDs for firms, presidents, the regimes' 65 leading firms and every named group.
5. **The probe says faces and flags are easy, and logos are where the work is.** All 60 most-followed US people have a free
   portrait, as do all 14 presidents since 1945 (public domain), all 6 lives candidates and all 16 state flags. Of the 25
   firms, 24 resolve by ticker. Of their 21 first-choice logos, 19 are public-domain text logos, but 13 are wordmarks from
   2.8:1 to 10.7:1, which are unreadable in a 24-40 px token. NVIDIA's logo carries Apache 2.0, which is not on the
   whitelist.
6. **A draft fetcher already exists:** `Tools/images/fetch.py` (untracked, 4 Nov 08:41). It resolves Wikidata image
   properties, checks licences and restrictions, crops with ffmpeg and writes a manifest with attribution. The probe found
   three gaps. Its restriction whitelist would reject the flags of Russia, Saudi Arabia and the USSR. Wikidata's first USSR
   flag is the 1922-23 one. It has no atlas packer and no QID resolution.
7. **Recommended storage:** pages of 128 px cells packed offline (ffmpeg, about 0.2 s a page) into a 2048 px RGBA PNG, with
   the cell's shape baked into alpha. Each page is imported from `Assets/Resources/Images/` with ASTC 4x4 on Quest and BC7
   on desktop, box-filtered mips and alpha dilation. One page holds 256 cells and costs 5.3 MiB of GPU memory. The tracked
   index JSON carries every cell's UVs and attribution. No atlas is built at load, and StreamingAssets is not used (section
   4.1 explains why).
8. **Recommended drawing:**
   - **Tokens:** one `Why/Thumb` mesh per layer, billboarded in the vertex shader, world-sized with pixel or degree clamps,
     ringed by the mark code (solid real, dotted modelled), one draw call.
   - **Label images:** `LabelSpec.Image` through the label system, for names that need collision handling.
   - **Panels:** uGUI images in the hover card and the panels, with a one-line credit under each.
9. **Attribution:**
   - the index JSON carries author, licence, licence URL, source page and the changes made;
   - a credit line sits under every image in a card;
   - an "Image credits" page opens from Help and from the hand menu (a kept canvas in XR);
   - a smoke check fails on any image without a complete record, and on any real person's image drawn for a synthetic life.

---

## 1. What the code can draw today

### 1.1 Objects are line drawings

Every economy object is built from `LineMeshBuilder` segments, polylines and rings (screen-space lines with pixel widths,
`Why/Line`) and `SurfaceMeshBuilder` bands (`Why/Surface`). The objects:

| Object | Where | How it is drawn | Size | Renderers |
| --- | --- | --- | --- | --- |
| A firm (25 towers) | HillActivityLayer.BuildCorporations (348-402) | halo: three gold rings (372-374) at the top of a beam 2 + 2.4 log10(1 + market cap) tall; beam, revenue ring, margin arc, $1T ticks ("controls"); an org chart of poles and ring heads ("orgs"); name (TMP) and a `<b>[TICKER]</b>` badge (TMP, scale .55, 376-377) | halo radius .45 + .09 sqrt(net income) units, about 1.4 for NVIDIA | 3 MeshRenderers, each with its own material, plus 2 TMP labels a firm |
| Owners' inner circle | InnerCircle (412-425) | a gold ring of 5-22 owner figures (a pole and a ring head each) round the halo, with a thread to the 1% cloud | ring radius 1.18 x halo + ... | one mesh for all firms |
| Cohort markers (household groups) | Finish (250-266), UpdateBalances (328-346) | a pole and a ring head coloured by motive; asset (gold) and debt (rose) rings; child poles close up | about 1.5 units tall at scale .5 | **640** (128 cohorts x 5 subgroups), each with its own mesh and material, rebuilt every replay year |
| Attention cloud | HillSocialLayer.Build (140-190), Star (374) | three crossed strokes and a ring; the top 16 in the "bright" mesh; names (TMP) for rank < 60 or fame > .6; owners of listed firms (Musk, Zuckerberg, Bezos, Gates, Cook) stand at their firm, officeholders at their seat | .22 + .7 x fame units | 4-6 meshes; one TMP a named star |
| Seats (presidents, legislators, reformers) | HillFoundationLayer.SeatActors (350-382) | a pole (1.4), a circle head (r .3) and a floor ring; label "Name\nOffice" | about 2 units tall | one mesh; one TMP a seat |
| The followed person (atom) | HillPersonLayer.Atom (193-201) | a pole, an arm bar, a wire-sphere head (MindDiagram.Sphere), a foot ring, wealth and debt rings | 1.55 units to the head | one mesh |
| The ladder's walker | HillPersonLayer.Ladder (403-406) | a pole, a sphere head and a gold ring | 1.75 to the head | one mesh |
| The dilemma's YOU and five counterparts | HillDilemmaLayer.Figure (266-271) | a pole 1.2 x scale, a wire-sphere head, a floor ring; labels "synthetic person #N" | 1.5 units to the head | one mesh for the arena |
| You | HillYouLayer.Figure (114-131) | a pole 2.2, a ring head, an arm bar and the cushion ring | 2.6 to the head | one mesh |

The retired bowl scene (InhabitantsLayer, SocialLayer, LandscapeLayer: `[GraphScenes(EconomyLayouts.BowlScene)]`) has its
own glyphs and is never drawn. M1-M3 (the stage, WP19-WP32) are not built.

### 1.2 Text

- World TMP labels in every hill layer:
  - raised to queue 3252 over a dark plate at 3251 (`Raised`, `Plate` in each layer);
  - the money chart's labels at 3250;
  - the wave's focus label at 3254.
- The label system (LabelSystem, with the HillLabels adapter of WP09):
  - role labels (title 18, name 15, value 13, note 12 px, clamped 12-22, at least 1 degree in a headset);
  - an icon left of the text;
  - plates (the 9-sliced `UiFactory.Rounded`), leaders and owner groups;
  - part alpha (emphasis keys) and clicks;
  - `MaxVisible` 110, role budget 140 (60 in XR);
  - drawn in TMP's own queue (3000) with sorting order 10 (plates and leaders 9).

### 1.3 Images and icons

| What | File | Facts |
| --- | --- | --- |
| Universe icons | Core/IconRaster.cs, Core/Icons.cs | 12 glyphs drawn as signed distance fields in C#: 128 px cells, 4 x 4 grid, a 512 px alpha atlas expanded to white RGBA32, 8% margin; box-filtered mips (cells are power-of-two aligned, so mips never mix cells); trilinear, mip bias -0.5. Sprites are made with `Sprite.Create` (one unit square), one shared material on built-in `Sprites/Default` (always included). Rasterized on the thread pool (`Prepare`), uploaded on first use. |
| Label icon slot | LabelSystem.cs: `LabelSpec.Icon` (50), `IconEm` 1.45 and `IconOnlyEm` 1.8 (107, 113), `Admit` (506), `Take` (722), `Assign` (1410), `SetColor` (1534; the icon takes the text's colour at 1551), `CreateIcon` (1634) | It resolves only `IconRaster.Ids` and logs a warning for any other id. The glyph fills 84% of the quad: about 16 px beside a 13 px label, 20 px alone. `HillLabel.Icon` passes through to it (HillLabels.cs 89, 292). |
| XR hand-menu icons | Xr/ChapterIcons.cs | line-art SDF icons, one 128 px RGBA32 texture each (not atlased), tinted by level colour |
| UI shapes | Core/UiFactory.cs `Rounded` | a 64 px 9-sliced rounded rectangle, generated |
| Imported textures | Assets/Branding (icon, splash) | the project's only imported textures, with default import settings (box mips, 2048 max) |

There is no photo, logo, flag or emoji anywhere in the economy, and the TMP font has no emoji glyphs: EVALUATION Q27 found
emoji in names rendering as boxes.

### 1.4 Rendering facts that bind any image design

- **Shaders.**
  - `Why/Line` and `Why/Surface` (Assets/Resources/Shaders) both carry the single-pass-instanced stereo macros, the
    highlight ids (`_WhyHi` ranges in WhyCommon.hlsl), `_Alpha` and `_Raw` (object space for hill layers).
  - The other shaders in use are TMP's and `Sprites/Default`.
  - Nothing samples a colour texture in the economy today.
- **Queues in the hills.**
  - Lines and fills use 3185-3248.
  - Plates are at 3251, raised text at 3252 and the wave's focus label at 3254.
  - The money chart and the regimes panel use 3240-3250 in their own views.
  - A token meant to stand over the lines belongs at about 3249.
- **No tonemapper on the hills** (decision 36). Bloom starts at 1.1. Marks are blended, not added, and are LDR unless lit.
  A photo must stay at or under 1.0.
- **URP** (Settings/WhyURP.asset): the SRP batcher is on, dynamic batching off, MSAA 4x and HDR on. SpriteRenderers on
  `Sprites/Default` are not SRP-batched, so each icon is its own draw.
- **Emphasis.**
  - `EconomyEmphasis.Apply/Set` switch a renderer on or off and set `_Alpha` through `GraphMaterials.SetAlpha`, so any
    shader with `_Alpha` works unchanged.
  - `EmphasisIds` gives every key a block of 4,096 highlight ids from 12,000,000: room for one id per token.
- **Quest** (Editor/QuestBuild.cs, ProjectSettings):
  - Build: IL2CPP ARM64, Vulkan, linear colour, single-pass instanced, 16-bit depth.
  - Android's default texture compression is format 3 (ASTC in Unity's enum).
  - Android's default quality level is "Medium" at full texture resolution (`globalTextureMipmapLimit` 0).
  - Decision 28 caps draw calls at M1 ≤ 12, M2 ≤ 20 and M3 ≤ 6 ("meshes merged per mark") and asks labels of at least 1
    degree (titles 1.2).
  - The HUD plane is 1.45 m away at 0.8 mm a canvas unit.
- **Picking.** `HillSocialLayer.Pick` reads `Mouse.current` directly. Picking new tokens should go through `PointerInput`
  (its `Position` and `WorldRay` serve hands too).
- **Pixels per unit** at 1080p with the 45° camera: about 1303.7 / distance (spec section 4). A 3-unit token at each view's
  distance (HillPresets):

| View | Distance | px for 3 units |
| --- | --- | --- |
| mobility | 76 | 51 |
| landscape, capture, strategy | 150 | 26 |
| people | 152 | 26 |
| social | 175 | 22 |
| foundation | 205 | 19 |
| companies | 235 | 17 |
| regimes | 300 | 13 |
| M3 (spec) | 330 | 12 |
| M2 (spec) | 435 | 9 |

---

## 2. The actors and the identifiers we already have

| Actors | Where in the data | How many | Identifier today | Gap |
| --- | --- | --- | --- | --- |
| Stored firms (towers) | circuit.json `capture`, corporate-programs.json `firms` | 25 | ticker (SpaceX: SPCX in corporate-programs, none in company-lives) | no QID |
| Company lives (lifelines, fates) | company-lives.json | 75 (the 25 plus 50: Enron, Lehman, Kodak, Sears, OpenAI, ...) | `id`, ticker for 72; many tickers are historic and reused today (S, Z, X, C) | no QID; name search plus a choice needed |
| Attention cloud | social/people.json | 1,074 accounts (1,047 persons, 27 groups); US 365, of them 359 adults alive in 2025, 354 with a QID; top 60 named; top 16 "bright" | `q` (QID) for 1,003; `handle` | none for the US named set |
| Owners at their firms | people.json `firms` | Musk (tesla, spacex), Zuckerberg, Bezos, Gates, Cook | QID + company id | none |
| Officeholders in people.json | people.json `gov` | 13 US (Musk, Trump, Obama, Harris, Ocasio-Cortez, Sanders, Vance, Cruz, Booker, Warren, Newsom, Schwarzenegger, Ivanka Trump) | QID | none |
| Presidents and reformers on the bedrock | government.json `actors` (civ united_states) | 27 rows: 19 presidents (Washington to Trump), 6 reformers (Douglass, Anthony, Tubman, Parks, Malcolm X, King), Eleanor Roosevelt, Ruth Bader Ginsburg | name and a figures.json id for some; no QID | QIDs (Wikidata P39 Q11696 resolves the presidents) |
| Real lives (M3) | social/visibility.json `people` | 6 candidates; approved: Musk Q317521, Trump Q22686, Winfrey Q55800 (decision 34) | `wikidata` | none |
| Owners named in hill-owners.json | economy/hill-owners.json | Zuckerberg (Meta); Page and Brin (Alphabet, shown jointly) | names | QIDs (Page, Brin) |
| World economies (M2) | global.json `economies` | 13 + EU27 + ROW (+ WLD) | ISO 3166-1 alpha-3 (USA, CHN, DEU, JPN, IND, GBR, RUS, CAN, MEX, KOR, TWN, SAU, BRA), EU27, ROW | QIDs (P298 resolves 13/13) |
| Regimes | regimes.json `regimes` | US, Sweden, China, Saudi Arabia, USSR (around 1980) | ids us, se, cn, sa, su | QIDs; the 1980 USSR flag must be pinned |
| Regimes' leading firms | regimes.json `ownership.firms` | 65 (US 22, Sweden 18, China 20, Saudi Arabia 5) | names only | QIDs by name search |
| Groups named in events | global.json `events` (68) | NATO, Quad, BRICS, G7 (via its loans, price cap), OPEC and OPEC+, EU, USMCA (an agreement), the UAE (leaving OPEC), the Houthis and the Kachin Independence Army (armed groups), QatarEnergy, Airbus and Boeing (companies) | party ISO codes only | QIDs; armed groups need a policy |
| US institutions | the bedrock, the money view, events | Congress, the Federal Reserve, the Supreme Court (the IEEPA ruling), the Treasury | none | QIDs |
| Collective-action examples being researched (research-collective cache: ozone) | Montreal Protocol data | up to 198 treaty parties, UNEP, CFC producers | ISO codes in the source data | a world flag page |
| Mobility places (Tools/history/sources.json) | Chetty: Charlotte 4.4%, San Jose 12.9%; Denmark 11.7%, Canada 13.4% | 2 cities, 2 countries | names | QIDs (cities: seal or flag) |
| Scientists in games.json | tournaments | Axelrod, Rapoport (winner entrant) | names | QIDs (optional) |
| Synthetic people | the dilemma's YOU, spouse, coworker, neighbor, boss, other tribe; M3's climber, stayer and faller; the striver #3505; You | about 10 roles | role names | generic figures only, never a photo |

### 2.1 What the probe found (19 cached requests, 4 Nov 2026)

The probe resolved each group by the route a pipeline would use:
- firms by ticker on an exchange (P414 with qualifier P249);
- economies by ISO code (P298);
- presidents by P39 = Q11696 since 1945;
- cloud and lives people by their stored QIDs;
- groups by name search (the top hit recorded, not trusted).

For each actor it took the first image property in fetch.py's order (person P18; company P8972, P154, P2910, P18; state
P41, P94, P158; group P8972, P154, P41, P2910, P94, P158, P18) and read that file's Commons metadata. "Free" means public
domain, CC0, CC BY or CC BY-SA, not non-free, as the extmetadata licence code gives it.

| Group | Actors | With a QID | With an image | Free | Licences | Restrictions |
| --- | --- | --- | --- | --- | --- | --- |
| Stored firms | 25 | 21 by a strict name match; 24 with a proper choice (AMD, Walmart and Exxon Mobil were right but failed the word match; Walmart's label sits in `mul`); BRK has no plain "BRK" ticker | 21 | 20 | PD 19 (text logos), CC BY 4.0 1 (Palantir: a third-party 32 px CoreUI icon, not the company's mark), Apache 2.0 1 (NVIDIA: not on the whitelist) | trademarked 20 |
| States | 16 (13 economies, EU, Sweden, USSR) | 16 | 16 flags | 16 | PD 16 | insignia 16; communist (China, USSR); **rusmilitarism** (Russia, USSR); **noresize** (Saudi Arabia); trademarked (Canada) |
| Presidents since 1945 | 14 (+1 spurious P39 match, "Phil Baker" Q107054330) | 14 | 14 | 14 | PD 14 (official portraits) | personality 3 (Carter, Clinton, Biden) |
| Cloud, top 60 US persons | 60 | 60 | 60 | 60 | CC BY-SA 4.0 13, CC BY 2.0 11, CC BY 4.0 10, CC BY 3.0 10, CC BY-SA 2.0 6, CC BY-SA 3.0 4, PD 4, CC0 2 | personality 9 |
| US cloud groups | 2 | 2 | 2 | 2 | CC BY 2.0 (a photo of Rhett & Link), PD (Dude Perfect's logo) | trademarked 1 |
| Lives candidates | 6 | 6 | 6 | 6 | Musk CC BY-SA 4.0, Trump PD, Winfrey CC BY-SA 2.0 (a 1997 photo), Gates CC BY 4.0, Bezos CC BY 2.0, Zuckerberg CC BY 2.0 | personality 2 |
| Groups and institutions | 10 searched | 10 (top hits right) | 9 | 9 | see the notes below | insignia 3, trademarked 1 |

Notes on the probe:
- **Firm logos are mostly wordmarks.**
  - Square-ish (aspect ≤ 1.7), 8: Apple, Microsoft and Meta (P8972 icons), Tesla, Mastercard, Palantir, Caterpillar,
    Chevron.
  - Wide, 13: J&J 10.7:1, Bank of America 9.9, SpaceX 8.0, Oracle 7.8, Broadcom 7.3, JPMorgan 7.0, NVIDIA 5.5, Alphabet 4.1,
    Netflix 3.7, Visa 3.1, Amazon 3.0, Costco 2.8, Lilly 1.8.
  - A 7:1 wordmark fitted into a 32 px token has letters about 4 px tall. The existing `[TICKER]` badge reads better.
  - Most of these logos are black, so on the dark scene they need a light tile behind them.
- **Portraits.** All 60 are at least 290 px on the short side (enough for 128-256 px thumbnails), and 31 file names are
  already "(cropped)". But some are not single-person portraits: Michelle Obama's P18 is "...greet Scott Kelly...", and
  Gigi Hadid's comes from a two-person photo. Full-body shots ("Stephen Curry Shooting") put the face in a small part of
  the frame. **Every crop needs a look**: fetch.py's "portrait" crop (the upper square, 12% from the top) is a heuristic,
  and there is no face detector here (no PIL, numpy or OpenCV).
- **Groups and institutions.**
  - OPEC logo PD (76 px SVG, so it scales).
  - NATO logo PD (insignia, 2:1).
  - BRICS: the first logo is the 2025 Brazilian chairmanship's (an event logo, not the group's).
  - G7: no image.
  - Quad: a meeting photo with people (rejected by the people rule).
  - Congress and Supreme Court: seals PD.
  - Federal Reserve: flag PD.
  - **UAW: its P18 is "Volkswagen union election.jpg", workers who are private individuals (must be rejected; no P154).**
  - AFL-CIO: its headquarters, CC0.
- **USSR.** Wikidata's first P41 is "Flag of the Soviet Union (1922–1923).svg"; the 1980 regime needs the 1955-1991 flag
  pinned.
- **Lag.** During the probe Wikidata's API refused requests at `maxlag=5` for over two minutes ("Waiting for wdqs1013: 8.7
  seconds lagged"). fetch.py's default maxlag of 5 will stall in such periods. The probe used 15 for its 19 reads.

---

## 3. Existing tooling: `Tools/images/fetch.py` (untracked draft, 846 lines)

What it already does:
- **Input:** an ITEMS.json list of `{id, kind, qid | file, prefer, crop, emoji, set}`, where kind is person, company,
  government, group or figure.
- **Wikidata:** reads the image claims, with `en|mul` labels and P1813 short names for monograms.
- **Commons metadata:** licence, restrictions, non-free and deletion flags, and depicts (P180) for photos.
- **Rules:**
  - a licence whitelist (PD, CC0, CC BY and CC BY-SA of any version, ported and IGO included);
  - restrictions: `trademarked`, `insignia` and `communist` are kept for an item's own mark, and `personality` only for a
    person's own portrait;
  - a person must be human, have an enwiki article, a known birth and be an adult;
  - a collective's photo is rejected when its text or depicts suggest people (Jev with `--jev`: noul ≥ 0.3).
- **Thumbnails:** fetched at Wikimedia's standard widths and processed with ffmpeg (SVG through librsvg). Crops are
  center, portrait or contain into a size x size square (256 by default): JPEG when opaque, PNG otherwise.
- **Generic figures:** OpenMoji 17.0.0 (CC BY-SA 4.0) or Twemoji 17.0.3 (CC BY 4.0), with brand glyphs refused.
- **Output:** `manifest.json` with author, licence, licence URL, attribution text, `credit_short`, the changes made, sha256,
  review notes and rejected candidates.
- **Etiquette:** cached by URL sha256; `--offline` rebuilds byte-identically from the cache.

Gaps for the app:
1. **Restriction whitelist.** It rejects `rusmilitarism` and `noresize`, so the flags of Russia, Saudi Arabia and the USSR
   would fall to monograms. The research-images agent should rule on these two classes, which are not in Commons'
   documented restriction table.
2. **Era.** No choice by date. Pin the USSR's 1955-1991 flag, and choose officeholders' and firms' marks by year where the
   scene is historic.
3. **QIDs.** No resolution (the items must arrive with QIDs). Section 4.5 adds `build-items.py`.
4. **Packing.** No atlas, no shapes, no monogram tiles, no contact sheet for review. Section 4.2 adds `pack.py`.
5. **Policy file.** It refers to `Docs/research/holistic/images.md`, which does not exist yet.
6. **Lag.** maxlag 5 stalls during database lag (section 2.1).

---

## 4. Proposal

### 4.1 Storage: where the thumbnails live

| Option | Load and memory | GPU format | Draws | Verdict |
| --- | --- | --- | --- | --- |
| **A. Pages packed offline into `Assets/Resources/Images/actors-<n>.png`, index JSON in `Assets/Resources/Data/images/`** | `Resources.LoadAsync` once at scene load; 5.3 MiB a 2048 px page with mips | ASTC 4x4 (Quest), BC7 (desktop), from the importer | 1 per page per mesh | **recommended** |
| B. One texture per actor in Resources, copied into a runtime atlas on the GPU (`Graphics.CopyTexture`, block-aligned 128 px cells of one format) | about 300 copies at load (tens of ms, spread over frames); same memory as A | compression kept | 1 per page | a later option if pages churn the repository; needs a spike on Quest's Vulkan copy support |
| C. One texture per actor, packed at load with `Texture2D.PackTextures` | sources must be readable (a CPU copy each); the atlas comes out RGBA32 unless every source is DXT: 21.3 MiB a page with mips; main-thread work | uncompressed on Quest | 1 per page | no ("an atlas built at load" costs 4x the memory on Quest) |
| D. Raw JPG and PNG in StreamingAssets, decoded with `ImageConversion.LoadImage` | Android reads StreamingAssets out of the APK through UnityWebRequest; each decode is main-thread work and RGBA32 | uncompressed | 1 per page after packing | no |
| E. A Texture2DArray (one slice an actor; the importer can split a flipbook grid) | as A | ASTC or BC7 | 1, up to 2,048 slices | only for tokens: SpriteRenderer and uGUI cannot sample arrays, so labels and panels would need A as well |

Layout:
- `Tools/images/actors.json` (tracked): the items, one per actor (section 4.5).
- `Tools/images/.cache/` (gitignored, like every Tools cache): API answers and the downloaded originals.
- `Assets/Resources/Images/actors-0.png`, `-1.png`, ...: the pages.
- `Assets/Resources/Data/images/actors.json` (tracked): the index the app reads: per id the page, cell, shape, kind,
  label, QID, `credit`, `attribution`, licence, licence URL, source page, author and changes made.
- `Docs/IMAGE-CREDITS.md`: generated from the index, the human-readable list.

Import settings are enforced by an `AssetPostprocessor` (new `Assets/Scripts/Editor/ThumbImport.cs`, `OnPreprocessTexture`
for `Assets/Resources/Images/`), so a reimport can never lose them:

| Setting | Value | Why |
| --- | --- | --- |
| Texture type | Default, sRGB | the app makes sprites with `Sprite.Create` (as Icons.cs does) |
| Alpha | `alphaIsTransparency` on | dilates colour into the masked corners, so mips have no dark fringe |
| Mips | on; mip filter Box | cells are power-of-two aligned, so box mips never mix cells until 1 texel a cell |
| Filter | trilinear; mip bias -0.5 (set on the material or texture, as Icons does) | tokens are small |
| Wrap | clamp | |
| Max size | 2048 | |
| Desktop | BC7 | 8 bpp, quality alpha |
| Android | ASTC 4x4 | 8 bpp; its 4 px blocks align with 128 px cells at every mip down to 4 px (6x6 blocks straddle cells) |
| Read/write | off | |

Memory and size:
- **A page:** 2048 x 2048 at 8 bpp is 4 MiB, plus a third for mips: 5.3 MiB on the GPU (RGBA32 would be 21.3 MiB).
- **Count:** about 250-330 actors fit one or two pages.
- **APK:** grows by the compressed pages, about 5-11 MB.
- **Repository size is the real cost.** `.git` is 15 MB today. A page PNG of 256 photo cells is about 6-9 MB (estimate:
  25-35 KB a 128 px RGBA photo cell), and every rebuild would add another.
  - Track the pages and rebuild them rarely, appending cells so their order never changes. Or generate them like the
    narration audio, which `.gitignore` already keeps out of Git ("generated, so they stay out of Git"), and keep only
    the index (with each original's sha1) tracked.
  - The second option also keeps CC BY-SA files out of the public repository.
  - The user decides (open question 1).

### 4.2 The atlas pages

- **Cells.** 128 px, 16 x 16 to a 2048 px page (256 cells). Content is inset 4 px, and every cell's shape is baked into
  alpha, so every consumer (a custom shader, Sprites/Default, UI/Default) draws it with no extra work.
- **Shapes by kind**:

| Kind | Cell | Shape |
| --- | --- | --- |
| Real person | face crop, reviewed | circle, 120 px |
| Synthetic or typical person | generic figure (Twemoji or OpenMoji) on a disc in the people colour | circle; the token's ring is dotted (the mark code's "modelled") |
| Company | logo fitted on a light tile (InkText #E8ECF2, for black marks); a monogram tile when the mark is wider than 2.5:1 at token size, or not free | rounded square, radius 16 |
| State | flag letterboxed at its own aspect, 1 px dark rule | rectangle with a 6 px radius |
| Group or institution | logo, seal or flag as above; a monogram (P1813 or initials) otherwise | rounded square |
| Place | seal or flag | rounded square |

- **Monograms** are baked offline in the app's own typeface (`/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf`
  through ffmpeg `drawtext`): the ticker for firms (the existing `[NVDA]` badge's words), P1813 or initials for groups.
- **Bigger cells.** A firm whose mark is a wordmark gets two cells: a monogram cell for tokens, and the wordmark at 256 x
  64 px in a wide cell (two 128 px cells side by side) for cards and panels, where it has room.
- **Packer** (new `Tools/images/pack.py`, ffmpeg only, as the tools are). It reads fetch.py's 256 px outputs and its
  manifest, then:
  1. crops and masks each cell (`geq` alpha);
  2. tiles each page with the `tile=16x16` filter (0.21 s for a 256-cell page in the prototype);
  3. writes the index and `IMAGE-CREDITS.md`;
  4. writes a **contact sheet** of every cell with its id and crop for review (the crop review is visual, and Jev cannot
     see images).
- **Order:** append-only by id; a removed actor leaves an empty cell until a deliberate repack.
- **Pages by scope:** page 0 for the economy's actors (firms, people, US institutions, figures); page 1 for the world
  (flags of every economy and treaty party, international groups). One token mesh reads one page.

### 4.3 Drawing

Three consumers share the index and the pages through a new `Core/Thumbs.cs` (modelled on Icons.cs):
- lookups by id (thread safe: the index is parsed on a worker): `Has`, `Cell`, `Kind`, `Credit`;
- the pages, loaded with `Resources.LoadAsync`, plus `GetSprite(id)` cached and the token material (main thread);
- `Ready`, so layers draw a neutral disc until a page arrives;
- helpers from the data's own keys: `Firm(ticker, name)` (as `CompanyLives.Matches`), `Person(qid)`, `State(iso3)`,
  `Figure(role)`.

Ids are namespaced: `firm:nvda`, `person:Q317521`, `state:CHN`, `group:opec`, `figure:coworker`, `place:charlotte`.

#### a. Tokens: objects in the scene (`Why/Thumb` shader, `ThumbMeshBuilder`)

- **One mesh per layer** (per page): 4 vertices a token, built on the layer's worker in its rebuild slot
  (`HillRebuilds.Take`).
- **Per-vertex attributes:**
  - centre (POSITION, land-local through `_Raw`);
  - corner (-1..1), world size, minimum and maximum pixels;
  - cell rect;
  - ring colour and alpha, ring width in px, ring style (solid or dotted);
  - saturation;
  - a highlight id from the layer's emphasis block.
- **Vertex shader** billboards toward each eye (single-pass-instanced macros, as Why/Line). The size is world units clamped
  to [min, max] px on a screen, and to at least `2·z·tan(deg/2)` in a headset (as `LabelSystem.DegreesPx` sizes labels:
  1 degree, 1.2 for a subject). So tokens behave as objects (nearer is bigger) but never shrink to specks.
- **Fragment shader** samples the page and computes the ring from the cell-local position: `frac(uv × 16)` gives it with
  no per-renderer data.
  - Saturation applies to photos only (logos, flags and seals are shown unaltered; section 4.4).
  - `_WhyHi` lifts a highlighted token's ring and `_WhyDim` dims the rest, as lines do.
  - `_Alpha` takes the emphasis, so `EconomyEmphasis.Apply` works unchanged.
  - Output is LDR: a photo never exceeds 1.0; only a hovered token's ring may bloom.
- **Blend** SrcAlpha / OneMinusSrcAlpha (blended, decision 36), ZWrite off, cull off, queue 3249: over every hill line and
  fill (≤ 3248), under the plates (3251) and raised text (3252) that name tokens.
- **Order within a mesh:** quads sorted by rank, least important first, at build; no per-frame sort.
- **Per frame:** only `_Alpha` (no allocation). A year swap rebuilds about 200 quads (well under 1 ms) and never touches a
  texture.
- **Picking:** each token's projected centre is tested against `PointerInput.Position` (hands too), with reused buffers,
  recomputed only when the camera or the pointer moves.
- **Sizes:** a token is about 3 units. That is 26 px from the landscape's 150 units, so clamps of 22-64 px on a screen and
  1.2-3 degrees in a headset would hold.
- **Costs:** one draw call per layer per page, against today's 640 marker renderers and 75 firm renderers.

#### b. Label images: names that need placement (`LabelSpec.Image`)

Where a thumbnail belongs to a name, the label system already solves collisions, budgets, HUD keep-outs, leaders, degrees
in XR and part alpha. Examples: an M2 block's "CHINA · $18.9T" with its flag; a regime button; a firm's name; an M3 life's
name. Changes:
1. **A separate `Image` field** on `LabelSpec` and `HillLabel` (an image id), so `Icon` keeps its meaning (a tinted
   glyph).
2. **`Admit` and `Take`** accept `Thumbs.Has(id)` and size the image at 2.2 em. A face needs 24-32 px; 2.2 em is about
   29 px at a 13 px label. The width estimate includes the image.
3. **`Assign`** sets the sprite from `Thumbs.GetSprite`. Image icons share one material on `Sprites/Default` with the
   page as texture.
4. **`SetColor`** gives an image `(1, 1, 1, a)`, not the text colour (line 1551 today tints the icon with the label's
   colour). The plate stays as it is.

Cost: one draw per placed image label (SpriteRenderer, not SRP-batched), held by the role budget (60 in XR). In M3 (cap 6)
the three portraits should be tokens, not labels.

#### c. Panels and cards

- **Hover card** (`HudTooltip`): an optional 64 px image left of the title, with the credit line (`credit_short`, for
  example "Photo: Gage Skidmore · CC BY-SA 3.0") under the body.
- **Panels:** the same image and credit in StrategyPanel (the focused firm), RegimePanel (each regime's button and the
  shown state), PersonPanel and SocialPanel. In LivesPanel and LivesStrip (M3), the three real lives' portraits head their
  rows; the synthetic lives get figures.
- **Drawing:** uGUI `Image` with sprites from the page, so a canvas batches them.
- **Wide wordmarks** are shown here (cards are 140-360 px wide), not in tokens.

#### Colour and meaning (decision 9: colour carries one meaning)

- **Ring colours from `EconomyStyle.Roles`:**
  - owners gold for firms and capital owners;
  - attention white for the cloud's people;
  - the state's light steel (`Government`) for officeholders and states;
  - people blue, dotted, for synthetic figures;
  - conflict violet only in a conflict scene.
- **Photos** sit at reduced saturation at context and support levels (for example .35 and .7) and in full colour as
  subject or when hovered. The credit records it ("shown desaturated when not in focus").
- **Logos, flags, seals and arms** are shown unaltered (their restriction templates ask it), so they fade by alpha only.
- **Generic figures** are tinted toward the people colour.

#### Budgets on Quest (decision 28)

| What | Cost |
| --- | --- |
| Tokens | 1 draw per layer per page, 4 vertices each (300 tokens are 1,200 vertices) |
| Label images | 1 draw each, within the role budget of 60 |
| Panel images | batched with their canvas |
| Texture memory | 5.3 MiB a page; nothing allocated per frame; no texture work in a year swap |

### 4.4 Attribution

- **Index record** (per image, from fetch.py's manifest):
  - id, kind, label, QID;
  - Commons file, source page and original sha1;
  - author, licence, licence URL;
  - `attribution` (full text: author, licence and link, source, the changes made) and `credit_short` (one line);
  - fetched date, restrictions, review notes.
  - For figures: the set, version, glyph code, page URL and licence.
- **In the app** (all reachable on desktop, in portrait and in the headset):
  1. **Under every image in a card or panel:** its `credit_short`.
  2. **"Image credits" page** (new `Economy/UI/ImageCredits.cs`):
     - every image in the build with its full attribution, grouped by people, companies, states, groups and figures;
     - opened from the Help sheet (H) and from the hand menu;
     - a kept canvas in XR (`XrSession.Keep`, which WP19 makes extensible);
     - links open with `Application.OpenURL` on desktop; in the headset the URL is printed.
  3. **Tour cards** that show a portrait large (M3's real lives) carry its credit in the footer.
- **Change notes per kind:**
  - photos: "cropped to a circle and scaled to 128 px; desaturated when not in focus";
  - logos: "scaled to fit a tile";
  - figures: "rendered from SVG at 128 px".
- **CC BY-SA thumbnails** are adaptations shared under their own licence; the credits page and IMAGE-CREDITS.md say so.
  The research-images agent should confirm the wording.
- **Checks** (a SmokeChecks rule, logged per capture: "[Why] images <view>: tokens N on P pages, label images L, missing
  M"). Each of these is an error:
  - an id drawn without an index record, or a record without licence, licence URL (unless PD or CC0), author (unless PD)
    and source;
  - a person image drawn for a synthetic life: `Thumbs.Figure` returns only kind `figure`, and the check also scans each
    token's id prefix;
  - a person image whose record lacks a QID or the public-adult check;
  - a player-build page that is not ASTC or BC7;
  - token draws in XR above the view's cap.

### 4.5 Pipeline: what it would take

1. **`Tools/images/build-items.py`** (new) writes `Tools/images/actors.json` from the data files, with how each QID was
   found:
   - firms: tickers through P414/P249, the candidates chosen by Jev (`Tools/jev.py`, as `Tools/social/judge.py` chooses
     between candidates) from our name, `sells` and `founded`; BRK through BRK.A or BRK.B or its name; historic firms by
     name search plus Jev;
   - people: `q` from people.json and visibility.json;
   - presidents: P39 Q11696, cross-checked against government.json names (the probe's spurious "Phil Baker" shows why);
   - states: P298 ISO codes;
   - groups, institutions and places: name search plus Jev;
   - each choice is recorded with its confidence; below a threshold it is left for review.
   - It also writes pins: the USSR's 1955-1991 flag; files where Wikidata's first choice is wrong (the BRICS chairmanship
     logo); the crop box after review.
2. **Approval.** The user approves the list of real people shown, as decision 34 did for the lives.
3. **`Tools/images/fetch.py`** (exists) fetches, checks and crops at 256 px, writing its manifest into `.cache`. Settle the
   restriction classes first, and run at off-peak hours or with a small maxlag change.
4. **Crop review:** the contact sheet. A reviewer (an agent can read images) fixes crop boxes in `actors.json`, then fetch
   runs again with `--offline`.
5. **`Tools/images/pack.py`** (new) writes the pages, the index, IMAGE-CREDITS.md and the contact sheet. The `--offline`
   rebuild is byte-identical.
6. **Unity** (one package):
   - `Core/Thumbs.cs`, `Core/ThumbMeshBuilder.cs`, `Resources/Shaders/WhyThumb.shader`;
   - `Editor/ThumbImport.cs`;
   - `LabelSpec.Image` (LabelSystem, HillLabels);
   - the HudTooltip image and credit;
   - `Economy/UI/ImageCredits.cs` plus Help and hand-menu entries;
   - `ImageChecks` (SmokeChecks);
   - emphasis rows for each layer's tokens (for example `activity.logos`, `social.faces`, `foundation.portraits`,
     `dilemma.figures`), through `EconomyEmphasis.Contribute` or WP14's retune.

Each layer then swaps its objects in its own migration package (WP15-WP18 own those files).

Scale of the first set:
- 25 firms;
- the top 60 US people (which include the 6 lives candidates);
- 14 presidents and about 8 reformers and justices;
- 16 states;
- about 10 institutions and groups;
- about 10 figures.

That is about 145 cells: one page. The 50 historic firms and the regimes' 65 firms would fill most of a second.

### 4.6 Where tokens would replace lines (candidates for the view designers)

| Today (lines) | Token | Views | Count | Draw calls |
| --- | --- | --- | --- | --- |
| Firm halo: three gold rings, plus the `[TICKER]` badge | logo or monogram tile, owners-gold ring sized by profit (the halo's own scale) | companies, landscape, capture, strategy, overview | 25 | 25 renderers become 1 |
| Cloud stars, top 16 to 60 | faces with a white ring sized by fame; the rest stay points | social (close), overview (top 16 only) | 16-60 | the bright mesh becomes 1 token mesh |
| Owners at their firm (Musk, Zuckerberg, Bezos, Gates, Cook) | face on the firm's inner circle | capture, companies, social | 5 | |
| Seats on the bedrock | presidents' official portraits; legislators' faces; reformers; steel ring | foundation, overview | 14 presidents (1946-2026), the year's few | |
| Dilemma's YOU and counterparts | generic figures (spouse, coworker, neighbor, boss, other tribe), dotted people-blue ring; real examples as states' flags or leaders' faces | dilemma, the collective-action scenes | 6 | |
| Cohort markers | one figure glyph tinted by motive (no photo) | people, rivers | 640 | 640 renderers become 1 |
| M2 blocks | flags in the block labels (label images), parties' flags on conflict marks | world | 16 (+ treaty parties) | |
| M3 lives | portraits of Musk, Trump and Winfrey at their money and visibility lines; figures for the climber, stayer and faller | lives | 6 | 1 token mesh (cap 6) |
| Regimes | flags on the regime buttons; leading firms' marks on the steel and gold summits | regimes | 5 + 65 | |
| Mobility scene | places' seals or flags (Charlotte, San Jose, Denmark, Canada); figures for typical lives | mobility | 4 | |

---

## 5. Risks and open questions

1. **Track the pages or generate them?** Tracked pages add about 6-9 MB to a 15 MB repository at each rebuild. Generated
   and gitignored pages (the narration precedent) keep the repository small and CC BY-SA files out of it, but a new
   machine must fetch the images by the index's sha1.
2. **The restriction classes `rusmilitarism` and `noresize`** (Russia, Saudi Arabia, USSR flags) and the `communist` flags
   (China, USSR): allow them for the state's own flag shown unaltered to identify it? (research-images)
3. **Firm wordmarks.** Monogram or ticker tiles in the scene, with the wordmark in cards only (proposed), or wide pill
   tokens? And NVIDIA's mark (Apache 2.0) and Palantir's (a third-party icon): monograms unless another free file is
   found.
4. **Group photos.** Michelle Obama's P18 shows others, and UAW's shows workers. The people rule plus Jev reject them;
   should a collective's photo ever be used? (UAW and AFL-CIO have no logo on Wikidata, so they would get monograms.)
5. **Armed groups and parties to a conflict** (the Houthis, the Kachin Independence Army): monograms only, or no image?
6. **Generic figures:** Twemoji (CC BY 4.0, flat colour, no share-alike) or OpenMoji (CC BY-SA 4.0, black outlines that
   vanish on the dark scene without a light disc)? Or procedural figure glyphs added to IconRaster (no licence, the line
   style)? The brief names emoji sets; the choice affects colour discipline.
7. **Photo dates.** Winfrey's portrait is from 1997 and Kendall Jenner's file is 290 px wide. Should the review prefer a
   recent photo, and must the image date print in the credit?
8. **Saturation at rest.** Desaturating photos changes them (the credit says so). Is full colour at subject level only
   acceptable to the user's eye?
9. **Faces in the replay.** The cloud's people exist only from 2010 (Instagram) and are a 2025 snapshot (decision 8). A
   face drawn in 1990 would be anachronistic, so tokens follow the same year rules as the stars.
10. **A spike for option B** (GPU-copied atlas) if pages churn; Quest's Vulkan copy of ASTC regions is untested here.

## 6. Files I read

- Assets/Scripts/Core: Icons.cs, IconRaster.cs, LabelSystem.cs (working tree), UiFactory.cs (Rounded), GraphLayer.cs
  (GraphMaterials), PointerInput.cs.
- Assets/Scripts/Economy: HillLabels.cs, Emphasis/EmphasisTable.cs, Emphasis/EconomyEmphasis.cs, HillPresets.cs,
  EconomyStyle.cs (Roles), Land/LandStyle.cs (queues), Draw/PaletteCheck.cs.
- Assets/Scripts/Economy/Layers: HillActivityLayer.cs, HillPersonLayer.cs, HillDilemmaLayer.cs, HillSocialLayer.cs,
  HillFoundationLayer.cs, HillYouLayer.cs.
- XR and builds: Assets/Scripts/Editor/QuestBuild.cs; Assets/Scripts/Xr/XrSession.cs, ChapterIcons.cs, XrRig.cs.
- Settings: Assets/Settings/WhyURP.asset; ProjectSettings/ProjectSettings.asset and QualitySettings.asset;
  Packages/manifest.json; .gitignore.
- Shaders: Assets/Resources/Shaders/WhyLine.shader, WhyCommon.hlsl.
- Data: Assets/Resources/Data/economy (corporate-programs, circuit, company-lives, global, regimes, games, hill-owners,
  groups); Data/government.json; Data/social (people, visibility); Tools/history/sources.json.
- Tools: Tools/images/fetch.py (untracked), Tools/social/wikidata.py, Tools/check-palette.py.
- Docs: Docs/hills-design/MODALITIES.md (sections 3.3, 4, 6, 7, 14) and MODALITIES-PLAN.md (WP09, decisions 8, 9, 25, 28,
  34, 36).
- Frames: Shots/base1/economy-view-{companies, dilemma, social, foundation, people, strategy}.png and player.log.
