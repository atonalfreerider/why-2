# Images of the actors: pipeline, policy and a sample run

Research for the images of the holistic views (role research:images, holistic views workflow, 5 Nov 2026). On 4 Nov
the user asked for "more image data to illustrate individuals, collectives (companies, governments, groups)", pulled
"from open sources". This file:

- describes the pipeline that does that, `Tools/images/fetch.py`, rewritten in place of the draft;
- sets the policy it applies and gives the sources the policy rests on;
- reports a run on the twelve sample actors the workflow named, plus four states that test the draft's gaps and eleven
  generic figures.

It builds on the earlier map, `maps/objects.md`, which probed 60 people, 14 presidents, 16 flags and 25 firms, and
corrects two of its findings (section 6).

**What exists now:**

- **The fetcher.** `Tools/images/fetch.py` (tracked, rewritten) has two commands. `fetch` resolves each actor, checks
  licences and restrictions, and writes 256 px thumbnails with a manifest. `pack` builds the atlas pages, an index,
  credits and a contact sheet.
- **The sample** is in `/home/john/Desktop/Unity/.why-2-build/work/holistic/images-sample/`, not in Assets:
  - `items.json`, the input;
  - `manifest.json` and the 26 thumbnails;
  - `atlas/`: `actors-0.png`, `index.json`, `contact.png` and `CREDITS.md`.
- **The scratch work** is in `work/holistic/research-images/`:
  - `cache/`, the fetcher's HTTP and Jev cache;
  - `web/`, every policy page read, with `index.json` holding URL and access date;
  - `tests/`, the negative tests.

**How it was checked:**

- **Rebuilds.** `fetch --offline` and `pack` rebuild all 31 output files byte for byte from the cache.
- **Negative tests.** Every rejection rule was tested with a real file (section 5.4).
- **Visual check.** The portraits, marks, figures, page and contact sheet were viewed. One portrait crop was fixed in
  review (Winfrey's: another guest's shoulder at the frame's edge).

## 0. In one page

1. **The pipeline works on all twelve samples.** Eleven get an image, all from Wikidata's own claims:
   - three portraits;
   - three logos;
   - four flags;
   - OPEC's logo.

   The United Auto Workers get a monogram. Their QID (Q1814260) was found by name search with Jev's choice (1.00).
   Their only image is a photo of workers voting, rejected under the people rule (Jev: 0.96 that private individuals
   are shown).
2. **Licences:** 13 of the 15 Commons images in the sample are public domain (the flags, the logos and Trump's
   official portrait). Musk's photo is CC BY-SA 4.0 (Gage Skidmore) and Winfrey's CC BY 2.0 (Maryland GovPics). The
   figures are CC BY-SA 4.0 (OpenMoji) and CC BY 4.0 (Twemoji).
3. **The draft's three gaps are closed.**
   - **Restrictions.** The two classes that blocked the flags of Russia, Saudi Arabia and the USSR are ruled on from
     the templates that set them. `rusmilitarism` is Template:Russian militarism symbol. `noresize` is not a
     restriction at all: it is a style class that six templates share, so the fetcher reads which template is on the
     file. On the Saudi flag it is Template:Shahada, whose text says use is permitted.
   - **Era.** Each image claim is chosen by the date it was in force. With `at: 1980`, or by default for a dissolved
     state, the USSR gets its 1955-1991 flag.
   - **QIDs and atlas.** QIDs now resolve from ISO codes, tickers and names, and `pack` writes the atlas.
4. **A file name is not an identity.** Commons' "UAW logo.jpg" passes every licence check (CC0), but it is the logo of
   the Union of Australian Women. The fetcher never searches Commons by name. A reviewed pin must also pass a Jev
   check, which rejected this file (another owner: 0.94).
5. **Wikidata lag.** Wikidata's maxlag includes its query service's update lag. On 5 Nov 2026 that was 109-119 s (the
   service was 1.8-2.0 hours behind), so polite `maxlag=5` reads were refused for hours. The fetcher waits up to
   `--patience` seconds, then stops with advice. A run someone waits on may omit maxlag, which is how the sample was
   fetched (section 3).
6. **Recommended generic figures:** OpenMoji's black line glyphs, turned into white masks that the app tints with the
   people colour. They match the line style, carry no skin or clothing colour (decision 9: colour carries one
   meaning), and the pipeline already handles them. The licence is CC BY-SA 4.0; Twemoji (CC BY 4.0, colour) is the
   alternative.
7. **Attribution is reachable in the app.** Every image has a title, author, source and licence record (TASL), a
   short credit line and a full attribution. The app needs three things: the credit line under every image in a card
   or panel; an "Image credits" page, from Help and the hand menu; and the restriction notices (section 4).

## 1. The pipeline

```
items.json ─► resolve QIDs ─► image claims, chosen for the date ─► Commons metadata ─► policy checks (licence,
restrictions, people, pins; Jev where a judgment is needed) ─► thumbnail at a standard width ─► ffmpeg: crop or
contain to 256 px ─► manifest.json   ─►   pack: cells with shapes in alpha ─► atlas pages, index.json, contact.png,
CREDITS.md   ─►   (Unity import and drawing: maps/objects.md section 4, not built)
```

### 1.1 Input: one object per actor

| Field | Meaning |
| --- | --- |
| `id` | lower-case slug, unique; becomes the file name and the atlas key |
| `kind` | `person` (a public figure), `company`, `government`, `group`, `place`, `figure` (a synthetic or typical person) |
| `qid` | the Wikidata item |
| `iso3` | ISO 3166-1 alpha-3 code, resolved through P298 (CirrusSearch `haswbstatement`); cross-checks a given QID |
| `ticker` | resolved through P414 (stock exchange) with qualifier P249 (ticker) on the query service; cross-checks a QID |
| `search` + `hint` | a name and what the actor is; with `--jev`, Jev chooses among the candidates (section 2.8) |
| `at` | a year, `YYYY-MM` or `YYYY-MM-DD`: the image in force then (default: today, or the day before a dissolved item's P576) |
| `file` + `as` | a reviewed Commons file pinned ahead of Wikidata's claims, and the property it stands for (P154, P41, P18, ...) |
| `crop`, `box` | `center`, `portrait` or `contain`; `box` = `[x, y, w, h]` fractions of the original, set in the crop review |
| `prefer` | a property order overriding the kind's |
| `monogram` | text of the fallback tile (default: ticker, an all-caps English short name such as USSR or OPEC, the ISO code, initials) |
| `approved` | for a real person: when and where the user approved showing them; `pack` leaves out a person without it |
| `emoji`, `set`, `variant` | figures: hex code, `openmoji` or `twemoji`, `color` or `mask` |

**Properties tried, by kind.** The first acceptable image wins:

| Kind | Property order |
| --- | --- |
| person | P18 (image) only, the person's own portrait |
| company | P8972 (small logo or icon), P154 (logo), P2910 (icon), P18 |
| government | P41 (flag), P94 (coat of arms), P158 (seal), P8972, P154, P2910, P18 |
| group | P8972, P154, P41, P2910, P94, P158, P18 |
| place | P41, P94, P158, P154, P8972, P18 |

**The era rule.** It closes the draft's USSR gap.

- **Ranking.** Claims in force on the date come first (P580 start and P582 end overlap the date), then property order.
  Within a property, the order is: larger overlap, then nearer in time, then preferred rank (only when the date is
  today), then a later end, then dark-on-light before light-on-dark (P8798), then Wikidata's order.
- **Undated claims** count as in force at any time.
- **Repeated files.** A file that stands in several claims is tried once, at its best rank. Russia's flag file is both
  the 1896-1918 and the post-1993 claim.

### 1.2 Output of `fetch`

- **Thumbnails:** `<id>.jpg` (opaque photos) or `<id>.png` (marks, figures, photos with alpha), 256 x 256 px.
  - Photos are center-cropped, or cropped to a reviewed box, or to the subject's region where Commons records one
    (P2677 on the depicts statement).
  - Logos, flags, arms and seals are contained whole on transparency, never cropped. A center crop would cut the
    canton off the US flag and the letters off JPMorgan's wordmark, and the insignia templates ask that such marks be
    shown unaltered.
- **`manifest.json`, per actor:**

| Group | Fields |
| --- | --- |
| Identity | `id`, `kind`, `label`, `qid`, `description`, `approved`, `resolved` (each method tried, its candidates, what was dropped and why, Jev's choice and probabilities), `era` (the date window and its basis) |
| Image | `file`, `property`, `rank`, `period`, `commons_file`, `source_page`, `title`, `original` (size, MIME, sha1, upload time, URL), `thumbnail_url`, `fetched_from` |
| Credit | `author`, `author_url`, `credit` (the source field), `date`, `licence`, `licence_code`, `licence_url`, `attribution_required`, `share_alike` |
| Policy | `restrictions` (the classes), `rulings` (class or template, and the ruling) |
| Changes | `crop`, `box`, `crop_basis`, `content` (the visible rect), `aspect`, `modified` (what was done to it) |
| Attribution | `attribution` (full text: title, author, licence and link, source, changes, restriction notices), `credit_short` (one line) |
| Integrity | `fetched` (retrieval date of the thumbnail), `size`, `bytes`, `sha256` |
| Review | `checks` (depicts, other people, people words, Jev values), `review` (notes for a human), `rejected` (every candidate tried before, with its reason) |

- **Failures and fallbacks.** An actor with no acceptable image gets `"file": null`, `"fallback": "monogram"` and the
  monogram text. `failures` lists every stage that failed (resolve, image, thumbnail, figure, policy) with its reason.

### 1.3 Output of `pack`

`pack` reads the manifest and the thumbnails, makes no network request, and writes `atlas/`:

- **`actors-<n>.png`**: 2048 px RGBA pages of 128 px cells (16 x 16), each cell's shape baked into alpha so every
  consumer (a token shader, `Sprites/Default`, uGUI) draws it as is:

| Actor | Cell |
| --- | --- |
| person | the photo masked to a 120 px circle |
| company or group logo | a light rounded tile (#E8ECF2, radius 16) with the logo fitted inside; black logos stay readable on the dark scene |
| a logo wider than 2.5:1 (a wordmark) | a monogram token (the ticker, e.g. JPM) **and** a two-cell 256 x 128 wide cell with the wordmark on the light tile, for cards and panels |
| flag | the flag at its own aspect, rounded corners, a 1 px dark rule |
| arms, seal | fitted as is |
| a collective's photo | a rounded square |
| no image | a monogram: dark tile (#2A3140), light text in Liberation Sans Bold, sized to fit |
| figure | the glyph fitted; `mask` glyphs are white for the app to tint |

- **`index.json`**: per id the page, global slot, column, row, span, `uv` (u0, v0, u1, v1 with the origin at the bottom
  left, as Unity uses; row 0 is the top of the PNG), shape, content rect, kind, label, QID, `approved`, and the
  attribution fields with the packing step added to `modified`. Monograms say they are not third-party images.
  `excluded` lists real people left out for lack of approval.
- **`contact.png`**: every cell on the scene's dark ground with its id, for the crop review. Jev cannot judge crops
  here (section 2.8), so a person looks.
- **`CREDITS.md`**: the full attributions grouped by kind, ready to become `Docs/IMAGE-CREDITS.md` and the in-app page.
- **Placement is append-only.** Tested: one actor removed and one added; 27 kept their slots, the removed actor's slot
  stayed empty (listed in `retired`), and the new actor took the next free slot. A deliberate repack deletes
  `index.json`.

### 1.4 Commands and reproducibility

```
python3 -I -B Tools/images/fetch.py fetch ITEMS.json --out DIR [--cache DIR] [--jev] [--maxlag 5] [--patience 600]
python3 -I -B Tools/images/fetch.py fetch ITEMS.json --out DIR --offline      # from the cache only
python3 -I -B Tools/images/fetch.py pack DIR [--cell 128] [--page 2048] [--allow-unapproved]
```

- **The cache** follows `Tools/social/wikidata.py`: one file per response, named by the sha256 of its URL, under
  `--cache` (default `Tools/images/.cache/`). Namespaces are `wikidata/`, `commons/`, `wdqs/`, `media/` and `jev/`.
  A `manifest.json` records each download's URL, retrieval date, sha256 and size, as `Tools/lives/build.py` does.
- **Media are keyed by URL plus the file's Commons sha1**, so a re-upload is fetched again.
- **Byte-for-byte output.** ffmpeg runs with bitexact flags and no metadata, so `--offline` reproduces every output.
  Tested on the sample: 31 of 31 files identical.
- **`.gitignore` needs one line from the lead:** `Tools/images/.cache/` (this agent may write only the two files
  named). Every run here used `--cache` outside the repository.
- **Tools:** ffmpeg 8.0.1 (with librsvg for SVG figures) and ffprobe; no PIL, numpy or OpenCV.

## 2. Policy

### 2.1 Licences

| Accepted | Detected by |
| --- | --- |
| Public domain, including the Public Domain Mark | extmetadata `License` `pd`/`pdm`, or `LicenseShortName` "Public domain" |
| CC0 | `License` `cc0` |
| CC BY and CC BY-SA, any version, ported (`-se`, `-de`) or IGO | `License` matching `cc-by(-sa)-N(.N)(-xx)`; the licence URL is derived when absent |
| A CC licence offered alongside another (multi-licensed files) | a licence category such as `CC-BY-SA-3.0`, because extmetadata reports one licence only and is unreliable for multi-licensed files (Extension:CommonsMetadata) |

**Rejected:**

- GFDL-only;
- the Free Art License (tested: rejected);
- the UK Open Government Licence;
- custom "Attribution" templates;
- Apache 2.0 (the map found it on NVIDIA's logo);
- any file marked `NonFree`;
- any file with a `DeletionReason` (nominated for deletion);
- any licence not in the list above.

Free does not mean on the list: the brief names four families, and the fetcher fails closed.

### 2.2 Non-copyright restrictions

Commons tags restrictions with templates. Each template adds a `restriction-<class>` CSS class, which the
CommonsMetadata extension reports in extmetadata `Restrictions`. Commons says these restrictions do not affect a file's
freedom and that reusers must comply with them. Commons:Machine-readable data documents 13 classes. The rulings:

| Class (template) | Ruling | Why |
| --- | --- | --- |
| `trademarked` (Trademarked) | identity | Identifying a company by its own logo, unaltered, in an educational view is referential use; no endorsement is implied |
| `insignia` (Insignia) | identity | Flags, arms and seals are shown unaltered, only to identify the state or body |
| `communist` (Communist symbol) | identity | China's and the USSR's flags identify the state; the bans the template lists concern propaganda and display contexts |
| `rusmilitarism` (Russian militarism symbol) | identity | Undocumented class. The template marks symbols of the Russian Federation and its armed forces, banned in some countries depending on context. Here Russia's flag only identifies a state in a data view |
| `noresize` | per template | Undocumented, and **not a restriction**: a layout class (`restriction-noresize`) shared by six templates. The fetcher reads which is on the file page |
| `noresize` (Shahada) | identity | The template says use of the image is permitted. Its ban concerns extremist groups' flags. Saudi Arabia's flag is shown unaltered |
| `noresize` (Palestinian insignia, SouthVietnam) | identity | National insignia, restricted only in specific contexts |
| `noresize` (Apartheid symbol, Anti-LGBT symbol, Vietnamese extremist symbol) | reject | Hate symbols, or symbols of groups designated as terrorist |
| `personality` (Personality rights) | portrait only | A public figure's own portrait, used to inform and never to endorse; rejected on any other image |
| `nazi`, `ihl`, `currency`, `design`, `costume`, `fan-art`, `2257`, `ita-mibac`, `aus-reserve` | reject | Outside the brief's uses; currency tested: rejected |
| any class not in this table | reject | Fails closed until ruled on here |

**On the three flags the map flagged:**

- **Russia's flag** carries `insignia` and `rusmilitarism`.
- **Saudi Arabia's** carries `insignia` and `noresize`, which resolves to Shahada.
- **The USSR's 1955-1991 flag** carries `communist` and `insignia` only. The `rusmilitarism` the map saw is on the
  1922-1923 flag, which the era rule no longer picks.

All three pass. Every "identity" image carries its notice in the attribution: for example, "The mark may be a
trademark of Apple; it is shown only to identify Apple", or "Contains the shahada, the Islamic declaration of faith;
shown unaltered only to identify Saudi Arabia".

**Rules for marks:**

- **Shown unaltered:** marks are never desaturated or recoloured. They fade by alpha only, as maps/objects.md proposed.
- **Armed groups** (the Houthis, the Kachin Independence Army in global.json): monograms only. The Shahada template
  itself lists Houthi flags among those banned in many countries.

### 2.3 Real people

- **Public adult.** A human (P31 Q5) with an English Wikipedia article and a birth date showing 18 or older. Otherwise
  the actor gets no image.
- **Own portrait only.** A person's image is only their own P18. If Commons' structured data lists other people as
  depicted, the record flags it for review.
- **Approval.** The three approved real lives (Musk, Trump, Winfrey; plan decision 34) carry `approved` in the
  sample. `pack` leaves out any person without it; tested: an unapproved Musk was left out and listed in `excluded`.
  `--allow-unapproved` exists only for review sheets.
- **Crop review.** Every portrait gets a look on the contact sheet. Winfrey's center crop showed another guest's
  shoulder, so a box was set: `[0.17, 0.074, 0.66, 0.472]`, a head-and-shoulders square ending left of the guest.
- **Never a stand-in.** A real person's photo never stands for a synthetic or typical life. Those get figures (2.6),
  and the maps/objects.md smoke check scans id prefixes for it.
- **Inform, never endorse.** The personality-rights template asks reusers to respect the rights of the people shown.
  The app uses portraits beside facts about the person, never beside a product or a claim of support.

### 2.4 Photos of collectives

- **Last resort.** A company's, state's or group's P18 photo comes after its logo, flag, arms and seal. The order ends
  with P18 for every kind.
- **Rejected when it may show people:** when the description, title or categories contain people words (workers,
  members, crowd, protest, voters and so on), or when Commons' structured data says a human is depicted.
- **Second opinion (`--jev`).** Jev also judges whether private individuals are shown; a photo is rejected at 0.3 or
  more. Jev sees only photos that could actually be used: none behind an accepted mark.
- **Calibration.** On the sample's 12 collective photos, the words and Jev agree on all 12:
  - 11 buildings, landscapes and maps: no flagged words, Jev 0.02-0.06;
  - UAW's "Volkswagen union election.jpg": "celebration", "workers", Jev 0.92 in that batch and 0.96 alone.

### 2.5 Logos and trademarks

- **Most logos are public domain because they are simple.** On Commons, PD-textlogo marks a logo as public domain when
  it is only simple shapes or text, below the threshold of originality. That threshold differs between countries, so a
  logo that is free in one can be protected in another. The `trademarked` notice travels with it either way.
- **Wordmarks are unreadable in a token** (a 7:1 mark is about 4 px tall in a 32 px token). The packer gives them a
  monogram token and a wide card cell. JPMorgan Chase's 2024 wordmark is 7.0:1. Walmart's own small icon, the spark
  (P8972, since 13 Jan 2025), is used directly.
- **Light-on-dark variants rank last** (P8798 Q6545942), because tiles are light. Apple's black logo is picked over its
  white one.
- **No free logo means a monogram.** Examples: NVIDIA's (Apache 2.0) and Palantir's (a third-party icon, per the map),
  and UAW's (none on Wikidata). A reviewed pin can substitute a free file, which must pass the Jev check (2.8).

### 2.6 Generic figures for synthetic and typical people

| | OpenMoji | Twemoji |
| --- | --- | --- |
| Version used | 17.0.0 (released 23 Apr 2026; npm `openmoji@17.0.0`) | 17.0.3 (tag `v17.0.3`, 1 Jun 2026; maintained by jdecked) |
| Licence | graphics CC BY-SA 4.0 (code LGPL-3.0) | graphics CC BY 4.0 (code MIT) |
| SVG source | `cdn.jsdelivr.net/npm/openmoji@17.0.0/{color,black}/svg/<HEX>.svg`: 4,495 glyphs each | `cdn.jsdelivr.net/gh/jdecked/twemoji@v17.0.3/assets/svg/<hex>.svg`: 4,009 glyphs |
| PNG source | release zips only: 618 px and 72 px, colour and black (`openmoji-618x618-black.zip`, 47 MB) | `assets/72x72/`: 72 px only, too small for 128-256 px cells |
| Metadata | `data/openmoji.json`: name, group, tags and the author of each glyph | none in the repository; names come from OpenMoji's file (same Unicode names) |
| Monochrome | yes: the `black` line set | no |
| Attribution asked | a credit line in an About section ("All emojis designed by OpenMoji – the open-source emoji and icon project. License: CC BY-SA 4.0") | a mention in an About or Legal section |

- **The pipeline uses SVG and renders it with librsvg at the cell size.** This avoids the 47 MB PNG zip, and Twemoji
  has no PNG large enough.
- **Twemoji file names** drop FE0F unless the sequence contains a zero-width joiner, as the Twemoji library does.
- **Brand glyphs are refused:** OpenMoji has 53 (E040 Twitter, E042 Facebook, ...); tested. So are OpenMoji's flag
  glyphs, since flags come from Commons.

**Recommendation:** OpenMoji black glyphs as white masks (`"variant": "mask"`). The last rows of the contact sheet
compare the three looks (`figure-coworker`, `-color`, `-twemoji`).

- **Why:** they are line drawings like the economy's own marks, and the app tints them (people blue, a dotted ring for
  "modelled", as maps/objects.md proposes). The colour sets add yellow skin and coloured clothes that mean nothing in
  the palette. Default emoji skin is a neutral yellow in both sets, and the person glyphs used are gender-neutral where
  Unicode has one.
- **The cost** is share-alike: each rendered glyph is CC BY-SA 4.0 and is credited per glyph with its author.

**Glyphs for the roles.** All are OpenMoji 17.0.0. The first nine are rendered in the sample.

| Role (where) | Glyph | Code | OpenMoji author |
| --- | --- | --- | --- |
| You (dilemma, You layer) | person | 1F9D1 | Johanna Wellnitz |
| Spouse (dilemma) | couple with heart | 1F491 | Lisa Schulz |
| Coworker (dilemma) | office worker | 1F9D1-200D-1F4BC | Benedikt Groß |
| Boss (dilemma) | person in tuxedo | 1F935 | Liz Bravo |
| Neighbor (dilemma) | house with garden (a place, not a person) | 1F3E1 | Martin Wehl |
| Other tribe (dilemma) | busts in silhouette | 1F465 | Johanna Wellnitz |
| Climber (M3) | person climbing | 1F9D7 | Johanna Wellnitz |
| Stayer (M3) | person standing | 1F9CD | Laura Dworatschek |
| Faller (M3) | person kneeling (no "falling" emoji exists) | 1F9CE | Laura Dworatschek |
| Striver | person running | 1F3C3 | Johanna Wellnitz |
| Factory worker | factory worker | 1F9D1-200D-1F3ED | Benedikt Groß |
| Farmer | farmer | 1F9D1-200D-1F33E | Benedikt Groß |
| Student | student | 1F9D1-200D-1F393 | Benedikt Groß |
| Retiree | older person | 1F9D3 | Johanna Wellnitz |
| Child | child | 1F9D2 | Johanna Wellnitz |
| Household | family | 1F46A | Lisa Schulz |
| Partners, a pair | people holding hands | 1F9D1-200D-1F91D-200D-1F9D1 | Fanny Jung |
| Strikers, a union's members | raised fist | 270A | Julian Grüneberg |
| Voter | ballot box with ballot | 1F5F3 | Marleen Heine |
| Spender | shopping bags | 1F6CD | Jonas Roßner |
| Owner, saver | money bag | 1F4B0 | Marius Schnabel |
| Firm, plant (when no firm is named) | factory | 1F3ED | Martin Wehl |
| Bank | bank | 1F3E6 | Martin Wehl |
| Government (generic) | classical building | 1F3DB | Martin Wehl |

The design agents choose among these.

### 2.7 Flags, arms and seals

- **Usually public domain.** All 16 states the map probed were, and all seven flags in this sample are. The marks of US
  federal bodies are usually PD as federal works; the Fed's flag and the Supreme Court's seal in the probe were.
- **Shown whole and unaltered:** contained at their own aspect, never cropped or recoloured.
- **Historical states** need `at` (the regimes view's USSR "around 1980"); the rule picks the flag in force.
- **Treaty parties** (the collective-action research's up to 198 Montreal Protocol parties) need nothing more than
  `iso3` per party. P298 resolves each in one cached search.

### 2.8 Where Jev decides, and where it does not

Jev (TypeSafe System One, through `Tools/jev.py`; its credential was found and never printed) is asked only for
semantic judgments. Exact identifiers, licences, restriction rulings, dates and geometry stay in code. Every answer is
cached and recorded in the manifest. In line with AGENTS.md, probabilities are evidence, gated by thresholds, not
authorization.

| Question | Primitive | Accept when | Sample result |
| --- | --- | --- | --- |
| Which name-search candidate is the actor? | Choice among the candidates, plus a companion Noul "is any of them the actor?" (TypeSafe's pattern, in place of a "none" option) | choice ≥ 0.5 and companion ≥ 0.7 | UAW: Q1814260, 1.00 and 0.99. "Apple" (a test): Q312 Apple Inc., 1.00 and 0.97, over Apple Records and the fruit |
| Does a collective's photo show identifiable private individuals? | Noul | reject at ≥ 0.3 | UAW's photo: 0.96 |
| Is a pinned file the actor's own image? | two Nouls: does it refer to the actor; does anything tie it to another owner | refers ≥ 0.5 and another owner < 0.5 | "UAW logo.jpg": 0.98 and 0.94, rejected. "OPEC Logo.svg": 0.99 and 0.04, kept |

**How the pin check got its shape.** A single question ("is it the actor's own image?") gave the Australian union's
logo 0.88, matching the initials in the title. Following TypeSafe's advice to ask one snap judgment per question and
combine factors in code, it was split into the two above.

**Without `--jev`, a name resolves only in one case:** one candidate of the right kind remains and its label is the name
exactly. Tested: UAW without Jev stops at resolution with its five candidates and asks for a QID. The earlier rule (a
unique exact label among several) would have taken the fruit for "Apple".

**Not used: Jev's Box primitive**, which finds an object in an image and could propose face boxes for the crop review.
Three reasons:

- TypeSafe labels images and Box "Dreamer-ready", not for production.
- It needs an opt-in images header that `Tools/jev.py` does not send.
- It would send photos of people to a third party, which the brief's rule on sending personal data counsels against.

The crop review stays visual.

## 3. Wikimedia etiquette

| Rule | Source (read 4-5 Nov 2026) | What the fetcher does |
| --- | --- | --- |
| A descriptive User-Agent with contact information; not a generic library string; "bot" suggested for automated agents | WMF User-Agent policy | `why-2-images-bot/0.2 (https://github.com/atonalfreerider/why-2) Python-urllib/3.12` |
| 2026 API rate limits: 200 requests a minute for an unauthenticated client with a compliant User-Agent; 10 a minute for an unidentified one; 429 with Retry-After when exceeded; wait at least 5 s without one | mediawiki.org Wikimedia APIs/Rate limits (marked new in 2026) | one request at a time, at least 0.35 s apart (at most about 170 a minute); 429, 500 and 502-504 retried after Retry-After or a doubling back-off from 5 s |
| Action API without authentication: concurrency 1, under 5 requests a second; batch; gzip; after a request that took over 1 s, wait 5 s | wikitech Robot policy; API:Etiquette | serial; titles and ids in batches of 50; `Accept-Encoding: gzip` on API calls only |
| Media: concurrency at most 2, at most 25 Mbps; only originals or standard thumbnail sizes; prefer thumbnails | Robot policy | thumbnails only, at standard widths (20, 40, 60, 120, 250, 330, 500, 960, 1280, 1920, 3840); the API rounds other widths up and direct requests at other widths are refused (Common thumbnail sizes, T414805) |
| maxlag=5 for non-interactive tasks; wait at least 5 s after a lag error; interactive tasks may omit it | Manual:Maxlag parameter | `--maxlag 5` by default, growing pauses up to `--patience` (600 s), then a clear stop |
| Query service: 60 s deadline; 60 s of processing per 60 s per client; 30 errors a minute; 5 parallel queries per IP | Wikidata Query Service/User Manual | one query for all tickers (batches of 40), cached |
| Cache what can be cached | API:Etiquette | everything cached; `--offline` |

**Observed on 5 Nov 2026:**

- **Wikidata.** Its maxlag errors were of type `wikibase-queryservice`, at 109-119 s; the query service was
  6,537-7,126 s behind, and Wikidata reports that lag divided by 60. Commons' lag was 0.34 s of ordinary database
  replication.
- **The default stops cleanly.** A test with the default `maxlag=5` waited 5, 10 and 20 s and stopped after 4 attempts
  (36 s), naming the cause and the `--maxlag 0` option. The sample was fetched with `--maxlag 0`, as the manual allows
  for a task someone is waiting on.
- **Volume.** The first full run made 42 requests in 31 s; reruns made none.
- **Thumbnails** came from `thumb.wikimedia.org` (17 downloads); originals are on `upload.wikimedia.org`.
- **Cache size.** The research cache holds 61 downloads, 28.7 MB, mostly two 8.2 MB entity batches: `wbgetentities`
  returns every claim of a large item such as the United States. If that grows, the image claims could come from one
  query-service query instead, accepting the service's lag.

## 4. Attribution in the app

**What the licences require:**

- **CC 4.0 (BY and BY-SA), section 3(a):** keep the creator's name, any copyright notice, a notice of the licence and
  of the warranty disclaimer, and a link to the material where reasonably practicable. Say whether it was modified. Name
  the licence and link it. Any reasonable manner suits the medium, including a link to a page that holds the
  information (3(a)(2)).
- **BY-SA, section 3(b):** an adaptation is shared under the same licence, or a later or compatible one.
- **CC 2.0-3.0** (Winfrey's photo is CC BY 2.0):
  - section 4 also requires the work's title where supplied, and the licence's URI with every copy;
  - a "Collective Work" (here the atlas and the app) does not take the licence of a work inside it;
  - on notice, the credit must be removed if the author asks.
- **Public domain and CC0** need no credit; Commons recommends it for provenance, so the app credits them as a courtesy.
- **Recommended form:** title, author, source, licence (TASL), per CC's attribution guide. A title is required up to
  version 3.0.

**What the pipeline already provides, per image:**

- the TASL record;
- what was changed: cropped, scaled, masked, packed;
- the restriction notices;
- `credit_short` for one line, e.g. "Photo: Gage Skidmore · CC BY-SA 4.0" or "Flag: public domain";
- `attribution` in full, e.g.:

> "Elon Musk (54816836217) (cropped 2) (b)" by Gage Skidmore, CC BY-SA 4.0 (https://creativecommons.org/licenses/by-sa/4.0),
> via Wikimedia Commons (https://commons.wikimedia.org/wiki/File:Elon_Musk_(54816836217)_(cropped_2)_(b).jpg). Cropped to a
> centred square and scaled to 256 px; packed into a 128 px atlas cell, masked to a circle. This thumbnail is shared under
> CC BY-SA 4.0.

**Where the app shows it** (this refines maps/objects.md 4.4):

1. **Under every image in a card or panel:** its `credit_short`.
   - The hover card (HudTooltip), StrategyPanel, RegimePanel, PersonPanel, SocialPanel, LivesPanel and LivesStrip.
   - Selecting the line opens the credits page at that entry.
   - Tokens in the scene (22-64 px) carry no line. That is reasonable to the medium, as CC 4.0's 3(a)(2) and CC 2.0's
     4(b) allow, because the hover card, one step away, carries it.
2. **An "Image credits" page:**
   - every image in the build, with its full attribution, grouped as `CREDITS.md` groups them;
   - the OpenMoji credit line once at the top of the figures group, then one line per glyph;
   - opened from the Help sheet (H) and the XR hand menu, as a kept canvas in XR;
   - links open with `Application.OpenURL` on desktop and are printed in the headset;
   - the licence URIs are listed, as CC 2.0 asks.
3. **Tour cards** that show a portrait large (M3's real lives) carry its credit in the footer.
4. **The repository:** `Docs/IMAGE-CREDITS.md`, generated from the index, and the README's credits.

**Store builds.** CC 4.0 forbids technological measures that stop recipients exercising the licence (section
2(a)(5)(C), and 3(b)(3) for BY-SA), and CC 2.0 forbids distributing a work under measures that control access
inconsistently with the licence (section 4). If the Quest build is ever distributed through a store that applies DRM,
this needs a look; public-domain and CC0 images are unaffected. One way to satisfy it is to keep the images and their
index available without DRM, for example in the public repository or as a download. This is not legal advice.

**Checks** (a SmokeChecks rule, as maps/objects.md 4.4 proposes; `pack` already enforces the first two). Each of these
is an error:

- a cell without licence, licence URL (unless PD or CC0), author (unless PD), source or sha256;
- a person cell without `approved` or a QID;
- a person image drawn for a synthetic life (by id prefix);
- an image drawn anywhere without its credit reachable.

## 5. Sample results

### 5.1 The twelve actors

| Actor (id) | Wikidata, resolved by | Image (property; size) | Licence | Author (Commons) | Restrictions → ruling | Crop, notes |
| --- | --- | --- | --- | --- | --- | --- |
| Oprah Winfrey (`oprah-winfrey`) | Q55800, given | "Pre Inaugural Reception (52639556983) (cropped).jpg" (P18; 837 x 1170; taken 18 Jan 2023) | CC BY 2.0 | Maryland GovPics | none | reviewed box: the center crop showed another guest; approved (decision 34) |
| Elon Musk (`elon-musk`) | Q317521, given | "Elon Musk (54816836217) (cropped 2) (b).jpg" (P18; 3363 x 4602; 21 Sep 2025) | CC BY-SA 4.0 | Gage Skidmore | none | center; approved |
| Donald Trump (`donald-trump`) | Q22686, given | "Official Presidential Portrait of President Donald J. Trump (2025) (3x4).jpg" (P18; 1536 x 2048; 2 Jun 2025) | Public domain | Daniel Torok | none | center; approved; Commons lists the photographer as depicted (a data error, flagged) |
| Apple (`apple`) | Q312; ticker AAPL agrees | "Apple logo black.svg" (P154 since 1998; 814 x 1000) | Public domain | Original: Rob Janoff | trademarked → identity | contained; light tile; the white variant ranks after (light-on-dark) |
| JPMorgan Chase (`jpmorgan-chase`) | Q192314; JPM agrees | "Logo of JPMorganChase 2024.svg" (P154; 512 x 73, 7.0:1) | Public domain | JPMorgan Chase & Co. | trademarked → identity | wordmark: monogram token "JPM" and a wide cell |
| Walmart (`walmart`) | Q483551; WMT agrees (its label is only in `mul`) | "Walmart spark (2025).svg" (P8972 since 13 Jan 2025; 512 x 577) | Public domain | Walmart | trademarked → identity | contained; light tile |
| United States (`united-states`) | Q30; USA agrees | "Flag of the United States (DDD-F-416E specifications).svg" (P41, preferred of 26, since 4 Jul 1960; 1.9:1) | Public domain | Vector file created by Dbenbenn, Zscout370, Jacobolus, Indolences, and Technion. | insignia → identity | whole, canton intact |
| Sweden (`sweden`) | Q34; SWE agrees | "Flag of Sweden.svg" (P41; 1.6:1) | Public domain | Jon Harald Søby and others. | insignia → identity | whole |
| China (`china`) | Q148; CHN agrees | "Flag of the People's Republic of China.svg" (P41 since 27 Sep 1949) | Public domain | Zeng Liansong | insignia, communist → identity | whole |
| OPEC (`opec`) | Q7795, given | "OPEC Logo.svg" (P154; 76 x 73 SVG) | Public domain | unknown | trademarked → identity | light tile |
| European Union (`european-union`) | Q458, given | "Flag of Europe.svg" (P41) | Public domain | User:Verdy p and 7 others | insignia → identity | whole |
| United Auto Workers (`uaw`) | **Q1814260** (American labor union, P31 trade union), found by search: 7 hits, 2 dropped as scholarly articles; Jev chose it over a 1945-46 strike and three locals | none | – | – | – | **monogram "UAW"**. The only image claim, "Volkswagen union election.jpg" (P18, CC BY-SA 4.0), was rejected under the people rule ("celebration", "workers"; Jev 0.96) |

### 5.2 The gaps, tested

| Actor (id) | Resolved | Image chosen | Why |
| --- | --- | --- | --- |
| Russia (`russia`) | Q159 from ISO RUS (the only match) | "Flag of Russia.svg", the preferred claim since 11 Dec 1993 | `insignia` and `rusmilitarism` → identity; the draft rejected it |
| Saudi Arabia (`saudi-arabia`) | Q851 from SAU | "Flag of Saudi Arabia.svg", preferred since 15 Mar 1973 | `insignia` and `noresize` → Template:Shahada → identity; the draft rejected it |
| USSR in 1980 (`ussr-1980`) | Q15180, `at: 1980` | "Flag of the Soviet Union.svg" (19 Aug 1955 to 25 Dec 1991) | covers all of 1980; the "dark version" (to 14 Aug 1980) covers 62% and ranks second; the coat of arms covers 1980 too but flags come first |
| USSR, no date (`ussr`) | Q15180 | the same flag | default date: 25 Dec 1991, the day before its dissolution (P576) |

All four are public domain. Their monograms, for when an image is not wanted, are RUS, SAU and USSR (USSR is preferred
over the ISO code SUN).

### 5.3 Figures

There are 11 figures, all rendered from SVG at 256 px:

- **Nine OpenMoji black masks:** you, spouse, coworker, boss, neighbor, other tribe, climber, stayer, faller;
- **the coworker in OpenMoji colour,** for comparison;
- **the coworker in Twemoji colour,** for comparison.

All are licensed as in 2.6. Viewed on the dark ground, the masks read clearly. The faller (kneeling) and the neighbor
(a house) are the weakest metaphors.

### 5.4 Failures and rejections

**In the sample:**

- **UAW: no usable image.** Its one image claim was rejected under the people rule (above), so it gets a monogram.
- **Trump: review note** (other people in structured data). The portrait is single-person, so the image is kept.
- **JPMorgan Chase: review note** (a wordmark). Handled by the packer.

No licence, restriction or thumbnail failure occurred among the twelve.

**Negative tests** (in `research-images/tests/`, sharing the cache):

| Test | Result |
| --- | --- |
| Pin "One US dollar 1917.jpg" on the United States | rejected: restriction `currency`; fell back to the US flag |
| Pin "'Wavestone' at Cornaseer.JPG" (FAL) on OPEC | rejected: licence not on the whitelist (FAL); fell back to OPEC's logo |
| Pin "UAW logo.jpg" (CC0) on the United Auto Workers | rejected by Jev (refers 0.98, another owner 0.94): it is the Union of Australian Women's |
| UAW by name, without `--jev` | not resolved: five candidates of the right kind; asks for a QID or `--jev` |
| "Apple" by name, with `--jev` | Q312 Apple Inc. (1.00; companion 0.97) |
| OpenMoji E040 (Twitter) as a figure | refused: a brand glyph |
| Musk without `approved` | fetched, left out by `pack`, listed in `excluded` |
| Default `maxlag=5` during the query-service lag | stopped after 4 attempts (36 s) with the advice |
| `pack` on a folder without a manifest | clean error |

**Bugs found and fixed while testing.** The first runs exposed three defects, each fixed and retested:

- **Property order and era.** Rank and end date competed across properties, so Sweden and the USSR got arms instead of
  flags.
- **Repeated files.** Two claims with the same file overwrote each other's verdicts, so Russia's flag came through its
  1896-1918 claim.
- **Jev ordering.** The private-people queue was built before pinned files were checked.

### 5.5 Sizes

- **Thumbnails:** 26 files, 281 KB in all: 0.8 KB (Russia's three stripes) to 24 KB (OPEC). Portraits are 17-23 KB as
  JPEG.
- **Atlas page:** one 2048 x 2048 RGBA PNG of 241 KB holding 29 of 256 slots.
- **Cost per cell as PNG:** a 128 px photo circle 28-33 KB, a flag 6 KB, a logo tile 4 KB, a monogram 3.4 KB. A full
  page of about 150 faces and 100 marks would be about 5 MB.
- **On the GPU,** per maps/objects.md, a page compresses to 5.3 MiB with ASTC 4x4 or BC7 and mips.
- **Time:** `pack` took 4 s for 28 cells (one ffmpeg run a cell).

## 6. Corrections to maps/objects.md

- **The USSR's "first" flag.** The probe reported Wikidata's first USSR flag as the 1922-23 one, but that came from the
  probe sorting claims alphabetically. Wikidata's own first P41 claim is the 1955-80 "dark version". The draft, which
  kept Wikidata's order, would have taken that for both 1980 and the default. Either way, the first claim was the wrong
  rule, and the era rule replaces it.
- **Winfrey's portrait is not from 1997.** The same alphabetical sort produced that. Wikidata lists three P18 claims,
  first a 2023 photo (CC BY 2.0), then 2016, then 1997. The map's open question on photo dates is moot for her.
- **USSR flag restrictions.** The 1955-1991 flag carries no `rusmilitarism`; only the 1922-23 flag does.

## 7. For the lead

- **Repository:**
  - Add `Tools/images/.cache/` to `.gitignore`.
  - Create `Tools/images/actors.json`, the production item list with QIDs, `iso3`, tickers, `at` for historic scenes,
    reviewed boxes and pins, and `approved` for each real person. maps/objects.md 4.5 describes `build-items.py`; the
    fetcher now does the QID resolution that script would have done.
- **Unity** (unchanged from maps/objects.md section 4):
  - `Core/Thumbs.cs` reading `index.json`;
  - a `Why/Thumb` token shader;
  - `Editor/ThumbImport.cs` (ASTC 4x4 on Quest, BC7 on desktop, alpha is transparency, box mips);
  - `LabelSpec.Image`;
  - the credits page;
  - the smoke checks.
  - The packer's cell shapes and UV convention match that plan.
- **Review steps per batch:**
  - look at `contact.png`;
  - set boxes for portraits that need them;
  - confirm each pin;
  - rerun with `--offline`.

## 8. Open questions

1. **Figures:** OpenMoji black masks (recommended; CC BY-SA, line style) or Twemoji colour (CC BY, flat colour)? The
   comparison is the last row of `contact.png`.
2. **The restriction rulings** keep Russia's (`rusmilitarism`), Saudi Arabia's (Shahada) and the communist flags for
   identification. The user or the lead may prefer monograms for any of them; one table entry changes it.
3. **Store distribution:** will the Quest build go through a store with DRM (section 4)?
4. **Should Jev also check every chosen image** with the pin check, not only pins? Wikidata's own claims can be wrong:
   the map found Palantir's P154 is a third-party icon. It would cost one batched request per run.
5. **The repository question from maps/objects.md** (track the atlas pages, or generate them and keep only the index)
   is still open. The pages are about 0.2-5 MB each.
6. **Which real people beyond the three approved lives** the scenes show (the presidents, the cloud's top 16, owners
   at their firms): each needs an `approved` entry.

## 9. Sources

Read on 4 Nov 2026 by the earlier attempt and on 5 Nov 2026 by this one. Copies and dates are in
`work/holistic/research-images/web/index.json`.

- **Wikimedia Foundation User-Agent policy.**
  https://foundation.wikimedia.org/wiki/Policy:Wikimedia_Foundation_User-Agent_Policy (4 Nov)
- **API:Etiquette.** https://www.mediawiki.org/wiki/API:Etiquette (4 Nov)
- **Robot policy.** https://wikitech.wikimedia.org/wiki/Robot_policy (4 Nov)
- **Wikimedia APIs/Rate limits.** https://www.mediawiki.org/wiki/Wikimedia_APIs/Rate_limits (4 Nov)
- **Manual:Maxlag parameter.** https://www.mediawiki.org/wiki/Manual:Maxlag_parameter (4 Nov). Observed lag: the API
  with `maxlag=-1`, 5 Nov.
- **Common thumbnail sizes.** https://www.mediawiki.org/wiki/Common_thumbnail_sizes (4 Nov)
- **Wikidata Query Service/User Manual, query limits.**
  https://www.mediawiki.org/wiki/Wikidata_Query_Service/User_Manual (5 Nov)
- **Extension:CommonsMetadata.** https://www.mediawiki.org/wiki/Extension:CommonsMetadata (4 Nov)
- **Commons pages:**
  - Commons:Machine-readable data (restriction classes): https://commons.wikimedia.org/wiki/Commons:Machine-readable_data (4 Nov)
  - Commons:Non-copyright restrictions: https://commons.wikimedia.org/wiki/Commons:Non-copyright_restrictions (4 Nov)
  - Commons:Reusing content outside Wikimedia and Commons:Credit line (4 Nov)
  - Commons:Licensing (4 Nov)
- **Commons templates:**
  - Trademarked, Insignia, Personality rights and PD-textlogo (4 Nov);
  - Communist symbol, Russian militarism symbol (and its layout, which sets `restriction-rusmilitarism`), Shahada (and
    its layout, which sets `restriction-noresize`), Restriction-Layout, Apartheid symbol, SouthVietnam, Palestinian
    insignia, Anti-LGBT symbol and Vietnamese extremist symbol (5 Nov);
  - which templates use each class: a CirrusSearch `insource:` search of the Template and Module namespaces (5 Nov);
  - which sit on each flag: `prop=templates` on the file pages (5 Nov).
- **Creative Commons legal codes:**
  - CC BY 4.0 and CC BY-SA 4.0: https://creativecommons.org/licenses/by/4.0/legalcode.txt and .../by-sa/4.0/legalcode.txt (4 Nov)
  - CC BY 2.0 and CC BY-SA 2.0: https://creativecommons.org/licenses/by/2.0/legalcode and .../by-sa/2.0/legalcode (5 Nov)
- **CC Recommended practices for attribution.**
  https://wiki.creativecommons.org/wiki/Recommended_practices_for_attribution (4 Nov)
- **OpenMoji:**
  - README and licence: https://github.com/hfg-gmuend/openmoji (4 Nov)
  - releases (GitHub API, 4 Nov)
  - npm `openmoji` 17.0.0 (4 Nov)
  - jsDelivr file list (5 Nov)
  - `data/openmoji.json` 17.0.0 (5 Nov, through the fetcher)
- **Twemoji:**
  - README, LICENSE and LICENSE-GRAPHICS: https://github.com/jdecked/twemoji (4 Nov)
  - releases (4 Nov)
  - npm `@twemoji/api` 17.0.3 (4 Nov)
  - jsDelivr file list for `v17.0.3` (5 Nov)
- **TypeSafe docs:**
  - https://docs.typesafe.ai/primitives.md (5 Nov)
  - https://docs.typesafe.ai/primitives/box.md (5 Nov)
  - https://docs.typesafe.ai/patterns/companion-noul.md (5 Nov)
- **Wikidata and Commons data for the samples:** through the fetcher's cache, 5 Nov 2026. Every answer is in
  `research-images/cache/`, with its URL and date in `cache/manifest.json`.
