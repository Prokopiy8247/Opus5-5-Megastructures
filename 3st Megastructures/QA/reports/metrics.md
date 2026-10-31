| Metric | Value | How obtained |
|---|---|---|
| Transcript folders | 1 | measured: glob *3rd-Megastructures* under ~/.claude/projects, confirmed by cwd field |
| Transcript files (main / subagent / workflow) | 2 / 7 / 51 | measured: *.jsonl files found recursively |
| Assistant messages (deduplicated) | 4,403 | measured: unique message.id with usage |
| Models | <synthetic> (14), claude-opus-5-5 (4389) | measured: message.model |
| Uncached input tokens | 8,932 | measured: sum usage.input_tokens |
| Cache read tokens | 1,180,865,437 | measured: sum usage.cache_read_input_tokens |
| Cache write tokens (5 min / 1 h) | 23,914,513 (21,539,315 / 2,375,198) | measured: usage.cache_creation_input_tokens and its ephemeral split |
| Output tokens (incl. thinking) | 7,531,313 | measured: sum usage.output_tokens |
| Total tokens | 1,212,320,195 | measured: sum of the four categories |
| Wall-clock time | 22:06:17 | measured: first 2026-10-29T19:15:02.902000+00:00 to last 2026-10-30T17:21:20.393000+00:00 transcript timestamp |
| Idle gaps > 10 min | 6 gaps, 14:11:17 | measured: consecutive timestamps further apart than 600 s |
| Active time | 7:55:01 | derived: wall-clock minus all gaps > 10 min |
| Cost: uncached input | $0.04 | estimated: tokens x $4.0/MTok |
| Cost: cache read | $236.17 | estimated: tokens x $0.2/MTok |
| Cost: cache write 5 min | $107.70 | estimated: tokens x $5.0/MTok |
| Cost: cache write 1 h | $19.00 | estimated: tokens x $8.0/MTok |
| Cost: output | $150.63 | estimated: tokens x $20.0/MTok |
| Total API-equivalent cost | $513.53 | estimated: sum of categories; prices from https://platform.claude.com/docs/en/about-claude/pricing accessed 2026-10-30 for claude-opus-5-5 |

Gaps longer than 10 minutes (UTC):
- 2026-10-29T19:15:04.181000+00:00 -> 2026-10-29T22:01:08.735000+00:00 (2:46:05)
- 2026-10-29T23:45:13.771000+00:00 -> 2026-10-30T01:41:35.490000+00:00 (1:56:22)
- 2026-10-30T05:41:35.142000+00:00 -> 2026-10-30T13:49:09.604000+00:00 (8:07:34)
- 2026-10-30T14:05:41.070000+00:00 -> 2026-10-30T14:41:09.820000+00:00 (0:35:29)
- 2026-10-30T16:30:52.754000+00:00 -> 2026-10-30T16:47:15.161000+00:00 (0:16:22)
- 2026-10-30T16:49:04.070000+00:00 -> 2026-10-30T17:18:29.138000+00:00 (0:29:25)
