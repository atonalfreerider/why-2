# Picture popups for the narration (7 Nov 2026)

The user's request, verbatim (with an OpenAI key file attached, `~/Desktop/openai-key.txt`, which scripts read at run time and
never print, commit or pass on a command line):

> you can stop the recording. we for sure need picture reference popups to go along with the narrations. let's source them, and
> wherever necessary let's generate through chatgpt image

The full-length recording of the universe walkthrough was stopped; the October videos are kept as they were.

## Plan

- Each of the universe tour's 66 stops (`Assets/Resources/Data/Walkthrough/tour.json`) shows one to three picture cards beside its
  narration: what the narration names (a galaxy, a fossil, an artefact, a person, a place, a map).
- Sourced first: free-licensed files on Wikimedia Commons (public domain, CC0, CC BY, CC BY-SA; NASA, ESA, ESO and museum
  images among them), licence-checked by `Tools/images/fetch.py`'s policy and credited on the card.
- Generated only where no free image shows the subject (an abstract idea of the graph, an event no camera saw and no free
  illustration shows): OpenAI's `chatgpt-image-latest` (listed on the account on 7 Nov 2026). Every generated picture is
  credited "AI-generated image" with its model, and its prompt is kept in the manifest; it is never presented as a photograph,
  a specimen or a historical record.
- People named in the narration are shown only through public-domain or free-licensed portraits, the user's request recorded as
  the approval in the manifest.
- `plan.json` holds the stop-by-stop choice, `manifest.json` (beside the images) the licences and prompts.
