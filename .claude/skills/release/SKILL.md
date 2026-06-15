---
name: "release"
description: "Release a new version of debug-mcp to NuGet and GitHub"
argument-hint: "Version number, e.g. v0.20.0 (omit for next patch/minor)"
user-invocable: true
---

# Release Skill — debug-mcp

## Overview

Releases are triggered by pushing a **git tag** matching `v*.*.*`. The `release.yml` workflow then builds, tests, packs, and publishes to **NuGet.org** and **GitHub Packages** automatically. A GitHub Release is created by the workflow (with auto-generated notes) and then updated with a hand-written description via `gh release edit`.

**SSH push does not work** in this repo's CI environment — use `gh release create` to create the tag and release atomically via the GitHub API.

---

## Step 1 — Verify CI is green

```bash
gh run list --limit 3 --branch main
```

Do **not** release from a red build. Fix the CI first.

---

## Step 2 — Determine the next version

```bash
git tag --sort=-version:refname | head -5
```

Versioning convention: **semver**, pre-1.0, minor bump for each feature/fix batch (e.g. `v0.18.0` → `v0.19.0`). Patch bumps (`v0.19.1`) for hotfixes only.

---

## Step 3 — Create the tag and GitHub Release via `gh`

> **Do NOT use `git push origin vX.Y.Z`** — SSH key auth fails in this environment.  
> `gh release create` creates the tag on GitHub and triggers `release.yml` in one step.

```bash
gh release create vX.Y.Z \
  --title "vX.Y.Z — <Short Feature Summary>" \
  --target main \
  --notes "placeholder"
```

This immediately triggers the `release.yml` workflow (build → test → pack → NuGet publish → GitHub Release asset upload).

---

## Step 4 — Write the release description

While the workflow runs (≈5 min), write the body. Format mirrors existing releases:

```markdown
## What's new

### Feature NNN: <Feature Name>

<2–3 sentence summary of what the feature does and why it matters.>

#### New tool: `tool_name`

<What it does, key parameters, example JSON response.>

---

### QA / Bugfixes

| Bug | Fix |
|---|---|
| description | fix summary |
```

Guidelines:
- Title format: `vX.Y.Z — <Primary Feature> [+ Secondary]`
- Lead with the **most impactful change** (new tool > evaluator improvement > bugfix)
- Include a JSON example for any new tool
- Table for bugfixes when there are 3+
- Match the detail level of `v0.18.0` / `v0.19.0` (see `gh release view vX.Y.Z`)

---

## Step 5 — Update the release body

The workflow's `softprops/action-gh-release@v2` with `generate_release_notes: true` will **overwrite** the body with auto-generated notes when it creates/updates the release. After the workflow completes, restore your hand-written description:

```bash
gh release edit vX.Y.Z --notes "$(cat <<'BODY'
## What's new
...
BODY
)"
```

Or equivalently via a temp file:

```bash
gh release edit vX.Y.Z --notes-file /tmp/release-notes.md
```

---

## Step 6 — Verify

```bash
# Workflow passed?
gh run list --limit 3 --branch main

# Release visible with correct body?
gh release view vX.Y.Z | head -40

# NuGet published? (takes 5–15 min to index)
# https://www.nuget.org/packages/debug-mcp/
```

---

## Full example (v0.19.0)

```bash
# 1. Verify CI
gh run list --limit 3 --branch main

# 2. Determine version
git tag --sort=-version:refname | head -3   # → v0.18.0, create v0.19.0

# 3. Create tag + release
gh release create v0.19.0 \
  --title "v0.19.0 — ReSharper Inspect + C# Expression Evaluator + QA Sweep" \
  --target main \
  --notes "placeholder"

# 4. (write notes while workflow runs)

# 5. Update body after workflow completes
gh release edit v0.19.0 --notes "$(cat <<'BODY'
## What's new
...
BODY
)"
```

---

## Workflow reference

| File | Trigger | What it does |
|---|---|---|
| `.github/workflows/ci.yml` | push to `main` | build + unit/contract tests on ubuntu/windows/macos |
| `.github/workflows/deploy-docs.yml` | push to `main` | Docusaurus build + deploy to debug-mcp.net |
| `.github/workflows/release.yml` | push of `v*.*.*` tag | build → test → pack → NuGet.org + GitHub Packages → GitHub Release |

**Secrets required** by `release.yml`: `NUGET_API_KEY`, `MATRIX_ROOM_ID`/`MATRIX_TOKEN`/`MATRIX_HOMESERVER` (optional Matrix notification).

---

## Checklist

- [ ] CI green on `main` before tagging
- [ ] Version bumped correctly (minor for features, patch for hotfixes)
- [ ] `gh release create` with placeholder notes (triggers workflow + creates tag)
- [ ] Release body written and applied via `gh release edit` after workflow
- [ ] `gh release view vX.Y.Z` looks correct
- [ ] NuGet package indexed (check nuget.org after 5–15 min)
