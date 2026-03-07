# Delivery Driver - Agent Working Notes

## Current Project Snapshot (2026-03-07)
- Active branch: `feature/ui-ux-improvements`
- Branch state: local and `origin/feature/ui-ux-improvements` are in sync
- Latest delivered commits:
  - `f7b3300` Integrate balance HUD into global UI flow
  - `6412d1f` Add configurable speed units across HUD and settings
  - `9d806c5` Tune hard brake detection thresholds

## Recent Functional Progress
- Added speed unit preference (`KMH`/`MPH`) in settings flow.
- Refactored and improved in-game speedometer UI behavior/visuals.
- Integrated `BalanceHudUI` into global UI coordinator lifecycle.
- Tuned hard-brake notification thresholds to reduce noisy triggers.

## Git LFS Policy (Decision)
- Keep LFS enabled for now.
- Current tracked file: `Assets/Scenes/Game.unity`.
- Current observed LFS usage is low (~126 MB in history), so immediate migration is not required.

## LFS Safety Rules
- Do not remove `Game.unity` from LFS unless requested explicitly by project owner.
- Push `Game.unity` only when there is a real scene/gameplay/UI layout change.
- Avoid committing editor-only/noise scene changes.
- Re-check LFS usage periodically (monthly or before large releases).
- If LFS usage approaches risk threshold (example: 2-3 GB), plan a controlled migration discussion first.

## Commit and Push Guidelines
- Group commits by logical scope (gameplay, settings/UI, scene/assets).
- Use clear commit messages describing user-visible impact.
- Before push:
  - verify `git status -sb` is clean
  - verify branch and upstream target
  - run `git lfs status` when scene/assets changed

## Notes for Future Agents
- If user asks for "all changes commit/push", prefer multi-commit logical split rather than one squash commit.
- If push rejects with `cannot lock ref ... expected ...`, first compare:
  - `git rev-parse HEAD`
  - `git rev-parse origin/<branch>`
  then re-evaluate before force operations.
