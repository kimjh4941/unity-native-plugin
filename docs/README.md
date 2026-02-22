# Published documentation (versioned output)

This directory contains the published documentation site.

## Directory structure

```text
docs/
  latest/
  <version>/
```

## Meaning of each folder

- `docs/<version>/`
  - Fixed snapshot of a released version (e.g. `docs/1.0.0/`).
- `docs/latest/`
  - Always refreshed from the highest semantic version under `docs/`.
  - `docs/latest/VERSION.txt` stores the latest version.

## Update command

```bash
./scripts/publish_docs.sh <version>
```

The command performs:

1. Copy `manual/<version>/` -> `docs/<version>/`
2. Find the highest `x.y.z` directory under `docs/`
3. Refresh `docs/latest/` from that highest version
4. Write the version to `docs/latest/VERSION.txt`

## Recommended entry files per version

- `index.md` (English)
- `index.ko.md` (Korean)
- `index.ja.md` (Japanese)
