"""Country (Wikidata English label) -> one of the five social regions: us, east_asia, south_asia, europe, row."""

EAST_ASIA = {"People's Republic of China", "China", "Republic of China", "Taiwan", "Japan", "South Korea", "North Korea",
             "Korea", "Hong Kong", "Macau", "Mongolia", "Empire of Japan", "Korean Empire"}
SOUTH_ASIA = {"India", "Pakistan", "Bangladesh", "Sri Lanka", "Nepal", "Bhutan", "Maldives", "Afghanistan",
              "British Raj", "Dominion of India"}
EUROPE = {"United Kingdom", "England", "Scotland", "Wales", "Northern Ireland", "Ireland", "France", "Germany",
          "West Germany", "East Germany", "Spain", "Portugal", "Italy", "Kingdom of the Netherlands", "Netherlands",
          "Belgium", "Luxembourg", "Switzerland", "Austria", "Denmark", "Norway", "Sweden", "Finland", "Iceland",
          "Poland", "Czech Republic", "Czechia", "Czechoslovakia", "Slovakia", "Hungary", "Slovenia", "Croatia",
          "Bosnia and Herzegovina", "Serbia", "Montenegro", "North Macedonia", "Albania", "Kosovo", "Greece", "Bulgaria",
          "Romania", "Moldova", "Ukraine", "Belarus", "Russia", "Soviet Union", "Russian Empire", "Estonia", "Latvia",
          "Lithuania", "Malta", "Cyprus", "Monaco", "Andorra", "San Marino", "Liechtenstein", "Yugoslavia",
          "Kingdom of Italy", "Kingdom of Great Britain", "Georgia", "Armenia"}
US = {"United States", "United States of America"}


def of(country):
    """Region of a country label, or None for an empty label."""
    if not country:
        return None
    if country in US:
        return "us"
    if country in EAST_ASIA:
        return "east_asia"
    if country in SOUTH_ASIA:
        return "south_asia"
    if country in EUROPE:
        return "europe"
    return "row"
