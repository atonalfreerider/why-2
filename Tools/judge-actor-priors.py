#!/usr/bin/env python3
"""
Generates Jev (TypeSafe System One) structured judgments for the Individual Actor Causal Predictor
(Assets/Resources/Data/economy/actor-priors.json):

  1. Economic river siphoning / upward-mobility channels (6 channels: Wage/Skill, Index Equity,
     Property/Home Ownership, Operating Business, Innovation/IP Product, Fame/Attention Platform):
     scored on upward_mobility_leverage, capital_barrier, network_dependency, downside_risk (0-4).
  2. Fame's dual role in economic mobility and overall happiness (5 visibility tiers from private
     citizen to global mega-influencer):
     scored on mobility_boost, happiness_contribution, fear_exposure, autonomy_preservation (0-4).
  3. Behavioral response archetypes under Fear vs Desire (6 frontier archetypes):
     Choice on primary_behavioral_response + Scores on cooperation_tendency & desire_vs_fear_dominance.

Reads credentials via Tools/jev.py (never printed or stored). The Unity scene reads the output JSON
locally and never calls the API at runtime.
"""
import concurrent.futures
import datetime
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "Assets", "Resources", "Data", "economy", "actor-priors.json")
sys.dont_write_bytecode = True
sys.path.insert(0, HERE)
import jev  # noqa: E402

CHANNELS = [
    {
        "id": "wage_skill",
        "name": "Specialized wage & career climb",
        "description": "Increasing human capital, credentials, or high-margin industry placement (e.g. tech, finance, health) to earn higher labor compensation without owning significant equity."
    },
    {
        "id": "equity_investing",
        "name": "Broad equity & capital compounding",
        "description": "Saving surplus income and allocating into diversified public equities, index funds, or retirement accounts to siphon corporate operating surplus over decades."
    },
    {
        "id": "property_ownership",
        "name": "Property & real-estate ownership",
        "description": "Acquiring a primary home or rental real estate using mortgage leverage to build home equity, hedge shelter costs, and capture appreciation or rent flows."
    },
    {
        "id": "operating_business",
        "name": "Small business & enterprise ownership",
        "description": "Founding or buying an operating business (services, trades, local commerce, agency) to retain owners' operating surplus rather than pure wages."
    },
    {
        "id": "innovation_ip",
        "name": "Innovative product, software, or IP creation",
        "description": "Creating scalable technology, software, media IP, or venture-backed products with near-zero marginal distribution cost positioned inside high-growth economic rivers."
    },
    {
        "id": "attention_monetization",
        "name": "Public fame & attention-platform monetization",
        "description": "Building a large social-media or media following to convert audience attention into sponsorships, creator revenue, brand equity, or deal flow."
    },
]

FAME_TIERS = [
    {
        "id": "private",
        "name": "Private citizen (anonymous)",
        "followers": "< 1,000",
        "description": "Ordinary household member with no public audience; social ties are family, coworkers, and local community."
    },
    {
        "id": "domain_specialist",
        "name": "Domain / local reputation",
        "followers": "1K - 25K",
        "description": "Known within a profession, industry niche, or local business network; benefits from trust and referrals while retaining normal privacy."
    },
    {
        "id": "niche_creator",
        "name": "Niche creator / rising public figure",
        "followers": "25K - 500K",
        "description": "Visible creator, founder, or commentator with monetizable audience reach, exposed to platform algorithm shifts and online scrutiny."
    },
    {
        "id": "major_figure",
        "name": "Major public figure",
        "followers": "500K - 20M",
        "description": "Nationally recognized entertainer, athlete, politician, or founder with high brand/deal leverage and continuous public scrutiny."
    },
    {
        "id": "global_icon",
        "name": "Global icon / mega-influencer",
        "followers": "20M+",
        "description": "Top-tier global celebrity or billionaire founder in the Instagram/social cloud with massive attention-to-capital conversion power, extreme status pressure, and minimal private anonymity."
    },
]

ARCHETYPES = [
    {
        "id": "distressed_thin_buffer",
        "name": "Thin-buffer / economically stressed actor",
        "state": "Bottom two income quintiles, less than 3 months financial runway, high debt service or job insecurity, high fear share of spending."
    },
    {
        "id": "aspiring_striver",
        "name": "Upwardly mobile striver",
        "state": "Middle income quintiles, moderate saving, strong future orientation and reason, seeking career switch, property, or side business to climb."
    },
    {
        "id": "leveraged_homeowner",
        "name": "Leveraged middle/upper-middle homeowner",
        "state": "Upper-middle income, owns a mortgaged home, steady wages in services or manufacturing, balances family security fears with status/lifestyle desires."
    },
    {
        "id": "river_builder",
        "name": "Business owner / economic river builder",
        "state": "Self-employed or founder capturing operating surplus in a productive industry, combines moderate-to-high capital with active network connections."
    },
    {
        "id": "capital_owner",
        "name": "High-agency capital owner (top 1-10%)",
        "state": "Multi-year financial cushion, significant capital income from equity/property/business, high reason and agency, focused on compounding and preserving wealth."
    },
    {
        "id": "attention_anchor",
        "name": "High-fame attention anchor",
        "state": "Large public following in the social cloud, monetizes visibility and brand equity, navigates both outsized economic upside and intense reputational fear."
    },
]

CHANNEL_QUESTIONS = {
    "upward_mobility_leverage": {
        "type": "score",
        "instructions": "How strong is the asymmetric upward economic mobility leverage of `channel` for an individual seeking to siphon value from a US economic river?",
        "criteria": [
            "Minimal leverage: linear income ceiling with almost no path to major wealth accumulation.",
            "Modest leverage: steady incremental improvement, rarely leaps multiple wealth strata alone.",
            "Moderate leverage: reliable multi-decade path to upper-middle wealth if sustained.",
            "High leverage: strong asymmetric upside capable of moving an individual into the top decile.",
            "Extreme Pareto leverage: uncapped winner-take-most upside capable of top-1% wealth creation."
        ]
    },
    "capital_barrier": {
        "type": "score",
        "instructions": "How high is the upfront financial capital or family-wealth barrier required for an individual to meaningfully enter `channel` in the US?",
        "criteria": [
            "Negligible barrier: accessible with almost zero savings or family wealth.",
            "Low barrier: requires modest savings, education credit, or basic equipment.",
            "Moderate barrier: requires meaningful down payment, multi-year savings, or credit access.",
            "High barrier: requires substantial personal capital, collateral, or affluent family backing.",
            "Very high barrier: largely restricted to already wealthy or institutional-backed actors."
        ]
    },
    "network_dependency": {
        "type": "score",
        "instructions": "How strongly does success in `channel` depend on social connections, alliances, distribution networks, or insider access?",
        "criteria": [
            "Very low: outcomes depend almost entirely on impersonal markets or standardized rules.",
            "Low: helpful referrals exist, but individual effort dominates.",
            "Moderate: professional networks, mentors, or local reputation meaningfully tilt outcomes.",
            "High: deal flow, customers, investors, or career breaks rely heavily on trusted networks.",
            "Critical: network centrality, gatekeepers, or audience reach largely determines success."
        ]
    },
    "downside_risk": {
        "type": "score",
        "instructions": "How high is the financial downside risk (capital loss, debt distress, or sharp downward mobility) if an attempt at `channel` fails?",
        "criteria": [
            "Minimal risk: principal and livelihood remain largely intact on setback.",
            "Low risk: temporary income dip or recoverable drawdown.",
            "Moderate risk: meaningful loss of savings or time, recoverable over several years.",
            "High risk: substantial debt, equity wipeout, or multi-year career/financial setback.",
            "Severe risk: high failure probability with potential insolvency or reputational ruin."
        ]
    }
}

FAME_QUESTIONS = {
    "mobility_boost": {
        "type": "score",
        "instructions": "How strongly does `tier` (level of public fame and social visibility) boost an individual's upward economic mobility and value-capture ability in the modern US economy?",
        "criteria": [
            "No boost: visibility plays no role in economic mobility.",
            "Small boost: localized trust and referrals modestly improve job or client opportunities.",
            "Clear boost: monetizable audience creates meaningful side or primary income streams.",
            "Strong boost: large following unlocks lucrative deals, equity stakes, and brand leverage.",
            "Transformative boost: global attention converts directly into multi-million or billionaire capital."
        ]
    },
    "happiness_contribution": {
        "type": "score",
        "instructions": "Considering empirical wellbeing research (autonomy, social belonging, financial security vs scrutiny, isolation, and hedonic pressure), what is the net long-term contribution of `tier` itself to overall personal happiness?",
        "criteria": [
            "Net negative: chronic loss of privacy, public hostility, and alienation outweigh material gains.",
            "Slightly negative or strained: constant status pressure and scrutiny frequently erode wellbeing.",
            "Neutral / mixed: modest benefits roughly offset by visibility pressures or no effect from fame.",
            "Moderately positive: respected standing and opportunity enhance wellbeing without destroying privacy.",
            "Strongly positive: visibility reliably increases deep long-term personal happiness."
        ]
    },
    "fear_exposure": {
        "type": "score",
        "instructions": "How intense is the fear-driven pressure (reputational loss, cancellation, safety threats, status anxiety, algorithm dependency) experienced by an actor at `tier`?",
        "criteria": [
            "Very low: virtually no public reputational or visibility-based threat.",
            "Low: confined to immediate workplace or local community reputation.",
            "Moderate: noticeable online criticism, audience churn, and platform dependency.",
            "High: constant public scrutiny, brand fragility, and personal security concerns.",
            "Extreme: relentless global surveillance, acute reputational/safety threats, and loss of normal life."
        ]
    },
    "autonomy_preservation": {
        "type": "score",
        "instructions": "How much personal freedom, privacy, and unmonitored everyday autonomy does an actor retain at `tier`?",
        "criteria": [
            "Very low: public life and security needs severely constrain everyday freedom.",
            "Low: constant recognition and brand expectations restrict speech and movement.",
            "Moderate: able to live mostly normally, though public persona imposes clear constraints.",
            "High: strong personal privacy with selective professional visibility.",
            "Full: complete everyday anonymity and freedom from public scrutiny."
        ]
    }
}

ARCHETYPE_QUESTIONS = {
    "primary_behavioral_response": {
        "type": "choice",
        "instructions": "Given `archetype`, which primary economic behavior is this individual most likely to prioritize over the next 3-5 years when weighing fear (avoiding loss/insecurity) and desire (seeking upward mobility/status)?",
        "criteria": {
            "defensive_retrenchment": "Cutting discretionary spending, prioritizing immediate survival/bills, avoiding risk to prevent ruin.",
            "debt_financed_consumption": "Using credit or low saving to maintain lifestyle, cope with stress, or chase short-term wants.",
            "skill_and_career_climb": "Switching industries, building credentials, or pursuing higher-paying employment.",
            "river_positioning_investment": "Allocating savings, time, or leverage into property, a business, or a scalable product to siphon value.",
            "capital_compounding": "Protecting and compounding diversified financial/equity/business assets with long-horizon deliberation.",
            "attention_monetization": "Expanding public visibility, audience loyalty, and brand/equity conversion while defending reputation."
        }
    },
    "cooperation_tendency": {
        "type": "score",
        "instructions": "In repeated economic and social interactions (tit-for-tat games with family, coworkers, partners, and institutions), how strongly does `archetype` tend toward long-horizon cooperation and trust-building?",
        "criteria": [
            "Very low: acute scarcity or zero-sum rivalry drives frequent defensive defection.",
            "Low: cautious and transactional; defects quickly under stress.",
            "Moderate: reciprocates cooperation with close allies but guarded toward strangers.",
            "High: invests in long-horizon reputation, reciprocal alliances, and generous tit-for-tat.",
            "Very high: deeply anchored in durable high-trust coalitions and institutional reciprocity."
        ]
    },
    "desire_vs_fear_dominance": {
        "type": "score",
        "instructions": "For `archetype`, where does their marginal economic capital allocation sit on the Fear (avoiding threat/loss) versus Desire (expanding toward growth/status/pleasure) spectrum?",
        "criteria": [
            "Overwhelmingly fear-driven: nearly all marginal capital goes to preventing immediate loss or insecurity.",
            "Mostly fear-driven: protective spending, debt service, and safety margins dominate.",
            "Balanced: roughly equal pull between security protection and aspirational desire.",
            "Mostly desire-driven: surplus capital actively chases growth, status, autonomy, or opportunity.",
            "Overwhelmingly desire-driven: strong security cushion frees marginal capital for ambitious expansion."
        ]
    }
}


def summarize_score(ans):
    return {
        "score": round(float(ans["score"]), 3),
        "confidence": round(float(ans.get("confidence", 0.0)), 3),
    }


def summarize_choice(ans):
    return {
        "choice": ans["choice"],
        "confidence": round(float(ans.get("confidence", 0.0)), 3),
        "probabilities": {k: round(float(v), 3) for k, v in ans.get("probabilities", {}).items()},
    }


def run_job(kind, item, questions):
    state = {kind: item}
    resp = jev.call(state, questions)
    return kind, item["id"], resp


def main():
    if not jev.available():
        sys.exit("Jev credential unavailable; cannot generate actor-priors.json")

    jobs = []
    for c in CHANNELS:
        jobs.append(("channel", c, CHANNEL_QUESTIONS))
    for t in FAME_TIERS:
        jobs.append(("tier", t, FAME_QUESTIONS))
    for a in ARCHETYPES:
        jobs.append(("archetype", a, ARCHETYPE_QUESTIONS))

    total_tokens = 0
    model_name = None
    channels_out = []
    fame_out = []
    archetypes_out = []
    results = {}

    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as ex:
        futs = [ex.submit(run_job, k, item, qs) for k, item, qs in jobs]
        for fut in concurrent.futures.as_completed(futs):
            k, iid, resp = fut.result()
            results[(k, iid)] = resp["answers"]
            model_name = resp.get("model") or model_name
            usage = resp.get("usage") or {}
            total_tokens += int(usage.get("total_tokens", 0) or (usage.get("input_tokens", 0) + usage.get("output_tokens", 0)))

    for c in CHANNELS:
        ans = results[("channel", c["id"])]
        channels_out.append({
            "id": c["id"],
            "name": c["name"],
            "description": c["description"],
            "leverage": summarize_score(ans["upward_mobility_leverage"]),
            "capitalBarrier": summarize_score(ans["capital_barrier"]),
            "networkDependency": summarize_score(ans["network_dependency"]),
            "downsideRisk": summarize_score(ans["downside_risk"]),
        })

    for t in FAME_TIERS:
        ans = results[("tier", t["id"])]
        fame_out.append({
            "id": t["id"],
            "name": t["name"],
            "followers": t["followers"],
            "mobilityBoost": summarize_score(ans["mobility_boost"]),
            "happiness": summarize_score(ans["happiness_contribution"]),
            "fearExposure": summarize_score(ans["fear_exposure"]),
            "autonomy": summarize_score(ans["autonomy_preservation"]),
        })

    for a in ARCHETYPES:
        ans = results[("archetype", a["id"])]
        archetypes_out.append({
            "id": a["id"],
            "name": a["name"],
            "primaryResponse": summarize_choice(ans["primary_behavioral_response"]),
            "cooperation": summarize_score(ans["cooperation_tendency"]),
            "desireDominance": summarize_score(ans["desire_vs_fear_dominance"]),
        })

    out = {
        "asOf": datetime.date.today().isoformat(),
        "model": model_name or "jev-latest",
        "tokens": total_tokens,
        "note": (
            "Structured TypeSafe Jev (System One) priors for the Individual Actor Causal Behavioral Predictor: "
            "economic river siphoning channels, fame's dual effect on mobility and happiness, and behavioral "
            "responses under fear vs desire. These are model judgments combined in code with calibrated "
            "simulation records (EconomicLives, Mobility, ValueCapture) and the social cloud (SocialPeople)."
        ),
        "channels": channels_out,
        "fameTiers": fame_out,
        "archetypes": archetypes_out,
    }
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1)
        f.write("\n")
    print(f"Wrote {OUT} ({len(channels_out)} channels, {len(fame_out)} fame tiers, {len(archetypes_out)} archetypes, {total_tokens} tokens, model={out['model']})")


if __name__ == "__main__":
    main()
