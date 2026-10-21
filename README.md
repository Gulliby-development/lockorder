<p align="center"><img src="docs/logo.svg" width="96" alt="" /></p>

# lockorder

`lockorder` is an offline command-line analyzer for lock traces. It helps developers debug deadlocks and establish a lock hierarchy for a code base by checking lock-order inversions, listing observed nesting edges, finding a compatible lock order, and summarizing per-thread activity. It reads the trace without changing it.

[![CI](https://github.com/Gulliby-development/lockorder/actions/workflows/ci.yml/badge.svg)](https://github.com/Gulliby-development/lockorder/actions/workflows/ci.yml) [![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Why

When a program deadlocks, the cause is usually two threads taking the same locks in opposite orders. Log every lock acquire and release from a wrapper around your mutex, and `lockorder` finds the inversions, shows which thread nested which lock inside which, and tells you whether one global lock order exists that every thread respects. It needs .NET 10 and nothing else.

## Install

Build the solution:

```console
dotnet build
```

Run the CLI with a command and its arguments:

```console
dotnet run --project src/Lockorder -- ARGS
```

## Usage

The four commands are `check`, `edges`, `order`, and `threads`. Their syntax is:

```text
usage: lockorder check [--ignore LOCK]... TRACE
       lockorder edges [--ignore LOCK]... TRACE
       lockorder order [--ignore LOCK]... TRACE
       lockorder threads [--ignore LOCK]... TRACE
```

`TRACE` is a path to a trace file, or `-` to read standard input. The CLI accepts repeatable `--ignore LOCK` and `--ignore=LOCK` options; ignored locks are omitted from replay and its counts. A value may also list several locks separated by commas: `--ignore cache,log` is the same as `--ignore cache --ignore log`.

This `trace.log` example has two workers that acquire the same locks in opposite orders, plus a janitor:

```text
# two workers, one that repeats the first order, and a janitor
w1 acquire db
w1 acquire cache
w1 release cache
w1 release db
w2 acquire cache
w2 acquire db
w2 release db
w2 release cache
w3 acquire db
w3 acquire cache
w3 release cache
w3 release db
jan acquire log
jan acquire db
jan release db
jan release log
```

`lockorder check trace.log` reports the inversion:

```text
trace.log:3: LK201 lock order inversion among cache, db (threads w1, w2, w3)
    cache -> db (first at line 7 by w2)
    db -> cache (first at line 3 by w1)
```

`lockorder edges trace.log` lists the edges and their provenance:

```text
FROM   TO     THREADS  LINE
cache  db           1     7
db     cache        2     3
log    db           1    15
```

`lockorder threads trace.log` summarizes each thread:

```text
THREAD  ACQUIRES  LOCKS  MAXHELD
jan            2      2        2
w1             2      2        2
w2             2      2        2
w3             2      2        2
```

`lockorder order --ignore cache trace.log` prints a compatible order after ignoring `cache`:

```text
log
db
```

## Trace file

A trace is UTF-8 text with one event per line. An event has exactly three tokens separated by spaces or tabs:

```text
THREAD acquire LOCK
THREAD release LOCK
```

Blank lines and lines whose first non-space, non-tab character is `#` are ignored. Names are 1 to 40 ASCII characters: a letter or underscore followed by letters, digits, underscores, periods, or hyphens. The words `acquire` and `release` are reserved. A leading UTF-8 BOM is allowed, and CRLF line endings are supported.

During replay, a thread may acquire a lock only when it does not already hold that lock and no other thread holds it. Each accepted acquire records an edge from every lock the thread currently holds to the newly acquired lock. A thread can release any lock it holds, in any order. Rejected acquires and releases without ownership produce findings without changing replay state; locks still held at the end also produce findings. Ignored events are skipped after the complete trace is parsed, so they do not contribute locks, edges, problems, or thread activity.

## Findings

The `check` command reports replay problems:

- `LK101`: a thread acquires a lock it already holds.
- `LK102`: a thread tries to acquire a lock held by another thread.
- `LK103`: a thread releases a lock it does not hold.
- `LK104`: a thread still holds a lock at the end of the trace.
- `LK201`: a strongly connected lock-order component has at least two locks, and its internal edges were recorded by at least two different threads.

The `edges` command lists each observed nesting edge, its distinct recording-thread count, and its first line. The `threads` command reports each thread's accepted acquires, distinct acquired locks, and peak number of locks held at once. The `order` command returns a compatible order of all known locks when the graph is acyclic; any cycle prevents an order, including one recorded by a single thread.

## Exit codes

- `0`: success.
- `1`: `check` found problems or inversions, or `order` found a cycle.
- `2`: usage or input error.

## Development

Verify formatting and run the tests:

```console
dotnet format --verify-no-changes
dotnet test
```

## License

MIT, see [LICENSE](LICENSE).

---

Built with BAKED, an open-source coding agent.
