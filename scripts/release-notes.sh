#!/usr/bin/env bash
# release-notes.sh — affiche la section d'une version du CHANGELOG, qui sert de notes à la
# GitHub Release du tag (`.github/workflows/release.yml`).
#
# Usage : scripts/release-notes.sh <version>        (ex. 0.1.0, sans le « v » du tag)
#
# Échoue, sans rien afficher sur la sortie standard, si la section « ## [<version>] » manque,
# est vide, ou n'a pas de rubrique « ### Migrations DB » remplie (au moins « aucune »),
# obligatoire dans chaque release (cf. CONTRIBUTING.md § Release & CHANGELOG).

set -euo pipefail

version=${1:?usage : $0 <version>}
changelog=${CHANGELOG:-CHANGELOG.md}

# Les lignes qui suivent « ## [<version>] », jusqu'au titre de niveau 2 suivant ou à une
# définition de lien (« [x.y.z]: https://… »), sans les lignes vides du début.
notes=$(awk -v heading="## [${version}]" '
  index($0, heading) == 1 { found = 1; next }
  found && (/^## / || /^\[[^]]+\]:[[:space:]]/) { exit }
  found && !started && /^[[:space:]]*$/ { next }
  found { started = 1; print }
' "$changelog")

if [ -z "$notes" ]; then
  echo "::error file=${changelog}::pas de section [${version}] dans ${changelog}, ou section vide" >&2
  exit 1
fi
# Le contenu de la rubrique : ses lignes non vides, jusqu'à la rubrique suivante.
migrations=$(awk '
  /^### Migrations DB[[:space:]]*$/ { found = 1; next }
  found && /^### / { exit }
  found && /[^[:space:]]/ { print }
' <<<"$notes")
if [ -z "$migrations" ]; then
  echo "::error file=${changelog}::la section [${version}] n'a pas de rubrique « ### Migrations DB » remplie (au moins « aucune »)" >&2
  exit 1
fi

printf '%s\n' "$notes"
