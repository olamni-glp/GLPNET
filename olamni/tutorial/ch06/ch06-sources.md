# Ch 6 Sources — Typed Programming

**PDF**: `GLP_ART.pdf`, book p 53 (PDF p 65). 

## ⚠ STATUS: CHAPTER IS A STUB IN THE BOOK

The PDF for Ch 6 contains **only the chapter title and section headings** — no body text, no Programs:

> **Chapter 6: Typed Programming**
> This chapter presents advanced GLP programming techniques that build on the moded type system introduced in Chapter 5.
>
> 6.1 Difference Lists
> 6.2 Quicksort
> 6.3 Equators: Emergency Brake
> 6.4 Bidirectional Communication
> 6.5 Buffered Communication

That's the entire chapter as published in `GLP_ART.pdf` (single page, p 53). All five sections are heading-only — no code blocks, no prose.

## Code-block index
**EMPTY** — there are no Programs in the book for Ch 6.

## Tutorial mode
cohesive-synthesis (charter assignment) — but the chapter cannot be specified yet because the source is not written.

## Required action before /tutorial-specify can run
Either:
1. Wait for the book Ch 6 to be filled in by the author, then re-scan and rewrite this file; OR
2. Reuse the typed Programs from Ch 5 (Quicksort §5.6) and §4.2 (buffered communication, bidirectional channels) as substitutes — but this is **synthesis from related chapters**, not extraction from Ch 6 itself, and must be acknowledged as such.

## Companion repo references (anticipated, by section title)
- §6.1 Difference Lists → `programs/typed_book/recursive/list_processing/` (difference-list idiom).
- §6.2 Quicksort → `programs/typed_book/recursive/list_processing/quicksort.glp` (already covered in Ch 5).
- §6.3 Equators → no clear analogue in repo yet.
- §6.4 Bidirectional Communication → `programs/typed_book/streams/buffered_communication/` (channel-based bidir).
- §6.5 Buffered Communication → `programs/typed_book/streams/buffered_communication/bb*.glp` (Ch 4 sliding-window buffer + typed variant).
- `../charter.md`
