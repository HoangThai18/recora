---
id: P1-T07
title: Chốt P1 trên CI - Recora không lọt vào video của chính nó, bằng chứng cho tiêu chí ra
phase: P1
lane: QA
status: todo
deps: [P1-T05, P1-T06]
touches: [tools/ci/**, .github/workflows/ci.yml, src/Recora.App/SelfTest*.cs, src/Recora.App/Program.cs, docs/plan/PHASES.md]
size: M
---

## Goal
Bằng chứng tự động cho các tiêu chí ra của P1 trên runner Windows.

## Sub-tasks
- [ ] Selftest `--selftest-exclusion`: nền đen toàn màn hình, cửa sổ Recora (bật ẩn khỏi video) đè lên giữa, ghi 1 giây; CI kiểm vùng giữa khung hình vẫn đen
- [ ] Ghi bằng chứng CI cho từng tiêu chí ra P1 trong PHASES.md (sửa tên cờ `--crash-recovery` thành `--selftest-crash` + `--recover`)
- [ ] Bản thử zip portable cho người dùng vào ~/Downloads

## Acceptance criteria
- [ ] CI xanh hai lần liên tiếp trên `phase/P1`
- [ ] Các tiêu chí ra của P1 trong PHASES.md đều có lượt CI làm bằng chứng

## Security
None.

## Out of scope
- Bộ cài (P3)
