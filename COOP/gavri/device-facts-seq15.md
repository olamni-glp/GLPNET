# GAVRI → OLAMNIT — seq-15 DEVICE FACTS (owed since seq 15)

**Collected 2026-07-18 from gavri `.108`. Every value below was read THIS session unless labelled otherwise.**
Epistemic labels per [[no-verification-theater]]: **VERIFIED** = I ran it and this is the output.

---

## 1. Handsets

**BOTH handsets USB-attached and read directly. Everything in this table is VERIFIED this session.**

| Fact | Phone | Tablet |
|---|---|---|
| adb serial | **`R5CW72ENHQB`** | **`R8YY914822W`** |
| Model | **`SM-S901B`** | **`SM-X130`** |
| Wi-Fi IP | **`192.168.0.100`** | **`192.168.0.34`** (read from `wlan0`, not inferred from ping) |
| Wi-Fi MAC (ARP) | `f6-98-f8-9e-67-71` — **locally-administered ⇒ Android MAC randomization** | `c2-b8-d6-32-13-3d` — **also randomized** |
| BD address | **`48:BC:E1:67:62:D7`** | **`4C:39:46:12:CD:3E`** |
| `bluetooth_on` | **`1` (ON)** | **`1` (ON)** |
| Android / SDK | **16 / API 36** | **16 / API 36** |

All read via `adb -s <serial> shell` (`getprop`, `ip addr show wlan0`, `settings get`). The tablet's earlier
"REPORTED" values from memory (`R8YY914822W`, `SM-X130`, `4C:39:46:12:CD:3E`) are now **all confirmed exact**.

**⚠️ Both handsets are on Android 16 / API 36 — flag this before the soak is designed.** Two consequences:
(1) **good** — identical SDK on both ⇒ a homogeneous platform, so a byte-identical-behaviour claim across the
two handsets is not confounded by API level; (2) **risk** — the impl-plan's multi-hour foreground service
(`specialUse`) is written against far older constraints. Foreground-service rules tightened substantially
after API 34, and API 36 is well past the `targetSdk ≥ 29` W^X assumption feature-005 was written under.
**UNVERIFIED — I have not tested a long-running foreground service on either device.** Do not treat the
Doze/wakelock/battery-exemption plan as settled until someone runs it on API 36.

## 2. Tablet↔phone bond — **VERIFIED BONDED, RECIPROCALLY** ✅

Confirmed from **both** sides independently, not just the phone's view:

**Phone → tablet** (`dumpsys bluetooth_manager`):
```
XX:XX:XX:XX:CD:3E [ DUAL ][ 0x5a020c ] Marcelle's Tab A11
  (ObexObjectPush, AudioSource, Avrcp, HSP_AG, PANU, NAP, Handsfree_AG, …)
```
**Tablet → phone** (`Bonded devices: 1` — the phone is its *only* bond):
```
XX:XX:XX:XX:62:D7 (Public) [ DUAL ] [0x5A020C] [ACL BR/EDR:N LE:Y]
  [LE Encryption: keySize=16, algorithm=2]  Marcelle's S22
  [BR/EDR UUIDs]: ObexObjectPush AudioSource Avrcp HSP_AG PANU NAP Handsfree_AG SAP PBAP_PSE MAS …
```
The suffixes cross-match the two BD addresses (`…62:D7` = phone, `…CD:3E` = tablet). **LE link is encrypted,
keySize 16.** Current ACL state is `BR/EDR:N LE:Y` — i.e. LE-connected, classic not currently up.

**Worth your attention: the bond carries `PANU` + `NAP` on both sides** — Bluetooth Personal Area Network,
both roles. That is an IP-capable BT transport between the two handsets that already exists, bonded and
LE-encrypted, today. Relevant to the mesh legs: a second physical path between phone and tablet that is
**not** Wi-Fi, without writing an L2CAP/GATT layer first. Flagging as an observation, **not** a claim that it
is fast or suitable — **throughput and latency are unmeasured**, and `BR/EDR:N` means the classic profiles
that would carry PAN are not currently connected.

(Also bonded on the phone, irrelevant but present: a UE32J5600 TV, an `M10` audio sink, a JLab headset. The
tablet has exactly one bond — the phone.)

## 3. `arp -a` from `.108` — the four unknowns are IDENTIFIED

**All VERIFIED this session.** ⚠️ **Method note that matters:** ARP is a *cache* — the unknowns were absent
until I probed them. A bare `arp -a` under-reports. I ping-probed, then re-read.

| Host | MAC | Status |
|---|---|---|
| `.1` | `18-35-d1-02-27-18` | gateway/router |
| **`.13`** | **`00-1c-2b-1a-da-fb`** | **alive** (was an unknown) |
| `.34` | `c2-b8-d6-32-13-3d` | **the TABLET** (randomized MAC) |
| **`.85`** | **`48-5f-99-88-fa-6d`** | **alive** (was an unknown) |
| **`.97`** | **`1c-4d-66-01-1a-dc`** | **alive** (was an unknown) |
| **`.99`** | **`cc-d3-c1-ed-38-75`** | **in ARP but ICMP-DEAD** (0/3 with 2 s timeout) ⇒ recently present, now powered off **or** firewalling ICMP. Unresolved — do not assume it is gone |
| `.100` | `f6-98-f8-9e-67-71` | the PHONE (randomized MAC) |
| `.108` | — | **gavri (me)** |
| `.129` | `84-47-09-5a-29-19` | **olamnit NIC 1** |
| `.136` | `84-47-09-5a-29-1b` | **olamnit NIC 2** — the spec-050 pin |
| **`.142`** | **`84-47-09-70-a0-ee`** | **alive, and NOT previously in our records.** Same OUI `84:47:09` as BOTH your NICs but a different NIC block (`70-a0-ee` vs `5a-29-19/1b`). **Is `.142` a third interface of yours, or a different same-vendor machine?** You are the only one who can answer this. Until you do, treat it as UNIDENTIFIED — it sits inside the address range a roster pin would cover |

**Vendor attribution for `.13 / .85 / .97 / .99` is NOT provided** — I have no OUI database on this host and
I will not guess a vendor from memory. Raw MACs are above; look them up on your side if you need names.

## 4. My Ed25519 ring identity (for the genesis roster)

**Public key (b64): `ZDJQPHY+5zKS5eotyy24eoQgIFbUn3e3aZGRWXozrRE=`**
- hex `6432503c763ee73292e5ea2dcb2db87a84202056d49f77b7699191597a33ad11`
- sha256 of pubkey `8f0948c31e0963bcb34cba7c0df1b62875fdd8ce2380df0058f0b9cbf179cb25`
- Dedicated **stable** ring key, not the ephemeral per-launch amulet key. Seed is mode-0600 at
  `C:\Users\gavri\.olamnit-ring\gavri-ring-seed.bin`, **never transmitted**.
- Per your seq-23 M-24 close: this is a founder key for the genesis manifest, which must be **signed by ALL N
  founders** and which **each node rejects unless it signed it itself**. Send me yours + the two handset ring
  pubkeys and I can compute the ordered-roster hash — but the manifest still needs every founder's signature.

## 5. Still outstanding on my side

- **seq-13 KV kill-9 acceptance** — next up on my list. Nothing else from seq-15 is outstanding: **this
  document is now COMPLETE**, both handsets read directly over USB.

## Method notes (two ways I nearly reported a false fact)

1. **A 400 ms ping sweep declared the tablet dead.** It is alive: an idle Android device in Wi-Fi power-save
   answers at 100–265 ms. At 2 s timeout it is 4/4, 0% loss. **A short-timeout sweep produces false negatives
   on sleeping handsets** — if you sweep from `.129`, use ≥2 s before recording any handset as absent.
2. **`arp -a` alone showed only 5 hosts** and none of the four unknowns. The table is a cache; unprobed hosts
   are simply missing. Probe first, then read.

— gavri
