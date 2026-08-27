# SLICE A - MEASURED HOST + TRANSPORT FACTS (first-party over SSH, 2026-08-25)

## Source: direct SSH into shiras, engineer-authorised. Every line below is command output.

### identity
```
shiras
Linux shiras 7.0.0-30-generic #30-Ubuntu SMP PREEMPT_DYNAMIC Fri Jul 31 18:22:54 UTC 2026 x86_64 GNU/Linux
 11:50:23 up 1 day,  2:25,  3 users,  load average: 1.20, 0.88, 1.38
uid=1000(shira) gid=1000(shira) groups=1000(shira),4(adm),24(cdrom),27(sudo),30(dip),46(plugdev),100(users),111(lpadmin),114(lxd),972(smbuser)
```

### mounts - what shiras can reach
```
/dev/sda2 on /mnt/biwin type ext4 (rw,nosuid,nodev,relatime,x-systemd.automount,x-systemd.device-timeout=10,x-systemd.idle-timeout=0,x-gvfs-show)
//gavri/GAVRI_D on /mnt/gavri/d type cifs (rw,relatime,vers=3.0,cache=strict
//gavri/GAVRI_D/ms-playwright/chromium_headless_shell-1208 on /mnt/gavri/d/ms-playwright/chromium_headless_shell-1208 type cifs (rw,relatime,vers=3.0,cache=strict
```

### board visibility
```
glpnet channel files: 302
board actors caps: ariellas gavriella olamnit shiras 
board actors ops : ariellas ariellas.hatzinor ariellas.yngenios-windows gavriella olamnit shiras 
```

### shiras own board writes
```
{"actor":"shiras","day":"2026-08-25","e_t_s":0.0,"engineer_id":"shiras","from_state":null,"op_id":"shiras:000001","op_type":"claim","seq":1,"timestamp":"2026-08-25T08:40:21Z","to_state":null,"workstation_id":"shiras-driver","wp_id":"wp-coordination-feature-stream-durable-superset-fix"}
{"actor":"shiras","day":"2026-08-25","e_t_s":0.0,"engineer_id":"shiras","from_state":"ready","op_id":"shiras:000002","op_type":"transition","seq":2,"timestamp":"2026-08-25T08:40:21Z","to_state":"claimed","workstation_id":"shiras-driver","wp_id":"wp-coordination-feature-stream-durable-superset-fix"}
```

### toolchain
```
bk-flow
breenlake
buildkit
buildkit-3rtask
buildkit-backlog
buildkit-beacon
buildkit-builder
buildkit-co
buildkit-codexreview
buildkit-colab
buildkit-commit
buildkit-constitution
buildkit-contributors
buildkit-deploy
buildkit-guardian
buildkit-guards
buildkit-help
buildkit-license
buildkit-marathon
buildkit-opskit
buildkit-owo
buildkit-plan-order
buildkit-proof
buildkit-push
buildkit-refine
---
git version 2.53.0
Python 3.14.4
```

### repos
```
/mnt/biwin/D_DRIVE/BSTDEV/research/buildkit/beacon/glpnet
/mnt/biwin/D_DRIVE/BSTDEV/research/crucible/glp/GLPNET
/mnt/biwin/D_DRIVE/BSTDEV/research/qhstate/vendor/glpnet-cs
branch: 095-shiras-glpnet-onboard-and-scheduler-rootcause
```

### THE SSH MESH README (verbatim, authoritative)
```
# Fleet SSH mesh — how to use it

Location: home directory of the SSH login user on every fleet host.
Last verified end-to-end: 2026-08-17 (60/60 routes, with negative controls).

---

## TL;DR

```
ssh olamnit                          # direct
ssh olamnit-via-ariellas             # one intermediate
ssh olamnit-via-ariellas-shiras      # two intermediates, in that order
```

**Never pass `-i`.** If you find yourself typing `-i <path>`, the config is wrong —
fix the config rather than working around it. See *Troubleshooting*.

---

## The hosts — NOT all the same OS

| host | IP | OS | login user | aliases |
|---|---|---|---|---|
| GAVRIELLA | 192.168.0.108 | Windows | `SMBUSER` | `gavriella`, `gavri`, `gavriellas` |
| OLAMNIT | 192.168.0.136 | Windows | `smbuser` | `olamnit` |
| ARIELLAS | 192.168.0.142 | Windows | `smbuser` | `ariellas`, `ariella` |
| **SHIRAS** | 192.168.0.170 | **Ubuntu Linux** | **`shira`** | `shiras`, `shira` |

> **SHIRAS is Linux and its user is `shira`, not `smbuser`.** Getting this wrong
> produces `Permission denied (publickey,password)`, which looks exactly like a
> missing key and sends you down the wrong path. It cost real time on 2026-08-17.

Every host trusts every other host's key — a **full mesh**, not hub-and-spoke.

## Multi-hop

Aliases exist for every permutation, in both orders:

```
ssh gavriella-via-olamnit                 # 1 hop:  here -> olamnit -> gavriella
ssh gavriella-via-olamnit-ariellas        # 2 hops: here -> olamnit -> ariellas -> gavriella
ssh gavriella-via-ariellas-olamnit        # same endpoints, reversed middle
```

Ad-hoc equivalent, for a route with no alias:

```
ssh -J olamnit shiras
ssh -J olamnit,ariellas shiras            # order is significant
```

These use OpenSSH `ProxyJump`. **The origin authenticates to *every* hop itself**, so
no private key is ever copied onto an intermediate and no agent forwarding is needed. A
compromised middle host cannot borrow your identity.

The corollary matters: **hopping gives reachability, never trust.** You cannot reach a
host through a jump unless that host already trusts *your* key. ProxyJump can never
bootstrap a new host into the mesh.

## Copying files

`scp`/`sftp` read the same config, so aliases and jumps work there too:

```
scp report.txt olamnit:D:/inbox/
scp -J ariellas file.txt olamnit:D:/inbox/
scp shiras:/home/shira/out.log .
```

---

## How it is wired

**Your private key:**

| host | key path |
|---|---|
| GAVRIELLA (as `gavri`) | `C:\Users\gavri\.ssh\id_ed25519_gavriella_client` |
| OLAMNIT / ARIELLAS | `C:\Users\smbuser\.ssh\id_ed25519_gavriella` |
| SHIRAS | `~/.ssh/id_ed25519` |

On the Windows hosts these are **non-default filenames**, so SSH will not offer them
automatically — that is the entire reason the config exists. SHIRAS uses the default
name and would work even without it.

**Public keys, by fingerprint** — verify against these rather than trusting a filename:

```
gavriella-client-to-peers      SHA256:qevr3nOmF16pEdsOnNmUO1F+DdvxiuPmdNNwL6cnDYg
smbuser@olamnit-to-GAVRIELLA   SHA256:2XDVGNP1pDXqwVwJHnMcramQ7r/4LGFL5tL/ZooIuys
smbuser@ariellas-to-GAVRIELLA  SHA256:CrRfUY/DGmyasTYHnyHheGgPY5F1oH6RAx01PNLlAA0
shira@shiras-smb-automation    SHA256:Q0Ad/fn83y7Pz4v+ncaqFXKlZybriqDzoxHuJMy9wEU
```

**Where trusted keys live — platform-specific, and a classic trap:**

- **Windows**: `C:\ProgramData\ssh\administrators_authorized_keys`, *not*
  `~/.ssh/authorized_keys`, because `smbuser` is a local Administrator on all three.
  **A key placed in the home directory is silently ignored for an admin account.**
```

---

# ADDENDUM A2 — MEASUREMENTS TAKEN AFTER THE FIRST CORPUS BUILD (2026-08-25T11:00-11:45Z)

## A2.1 THE PLATFORM DECLARATION — the host declared itself in the primary record
```
caps/shiras/shiras-caps-000001.jsonl   lww_ts 2026-08-25T07:46:46Z   18 enumerated records
  skill  linux-host          verified=True     <-- THE PLATFORM DECLARATION
  skill  distributed-host    verified=True
  role   builder
  tool   buildkit-scheduler buildkit-marathon buildkit-roadmap buildkit-release
         buildkit-registry buildkit-size buildkit-retrospective
  skill  bk-flow bk-marathon bk-proof bk-roadmap bk-implement bk-specify bk-plan bk-tasks
```
The declaration is smuggled into kind=skill because the capability vocabulary HAS NO PLATFORM KIND.
It is correct, machine-readable, and NO MATCHER READS IT. Every caps record from every actor on
every board carries verified=true with evidence=null - a self-report wearing an attestation.

## A2.2 THE LOGIN-SHELL PATH GAP (measured both sides)
```
ON SHIRAS (Linux):
  ssh shiras 'command -v buildkit-scheduler'            -> NOT FOUND
  ssh shiras "bash -lc 'command -v buildkit-scheduler'" -> /home/shira/.local/bin/buildkit-scheduler
  ~/.profile sets PATH for LOGIN shells only; ~/.bashrc returns at its non-interactive guard
  before reaching its own PATH export. Engine there: 2026.8.24.5 (NEWER than the fleet pin).

ON ARIELLAS (Windows) - the control:
  plain    : /c/Users/ariel/.../Python314/Scripts/buildkit-scheduler
  bash -lc : /c/Users/ariel/.../Python314/Scripts/buildkit-scheduler   IDENTICAL
  => NO differential on Windows. The defect is STRUCTURALLY LINUX-ONLY.
```

## A2.3 THE BOARD IS THREE DIVERGENT BOARDS (decisive, same command, same minute)
```
ARIELLAS leg  D:\coop\glpnet\sched            wp=3   {backlog 3}
GAVRI leg     \192.168.0.108\GAVRI_D\...     wp=32  {backlog 23, claimed 1, done 1,
                                                       escalated 1, in-progress 4, ready 2}
OLAMNIT leg   \192.168.0.129\Olamnit_D\...   wp=6   {backlog 6}
```
Three boards, one name. The ENTIRE ready/claimed/in-progress lifecycle exists on ONE of the three.
shiras presence differs per root: GAVRI ops=2 caps=1 cal=1; ARIELLAS and OLAMNIT: nothing.
shiras itself published this pattern at 08:15Z, before any lane inferred it, and drew the correct
conclusion: "An allocator with nothing in ready allocates nothing. It is behaving correctly."

## A2.4 SHIRAS IS AN ACTIVE PARTICIPANT, NOT A CANDIDATE
```
onboarded  07:46:46Z   claimed wp-coordination-feature-stream-durable-superset-fix 08:40:21Z
transitioned ready->claimed in the same op batch; engineer_id=shiras workstation_id=shiras-driver
GLPNET checkout: /mnt/biwin/D_DRIVE/BSTDEV/research/crucible/glp/GLPNET
                 branch 095-shiras-glpnet-onboard-and-scheduler-rootcause
It reaches 41 boards across 3 rw CIFS mounts and can mkdir caps/<actor> on all of them.
caps/shiras exists on only 2 of 41 -> the difference is per-invocation, not per-host-capability.
```
