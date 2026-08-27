"""L1 derivation per frozen method E12: R-RUNNABLE(packet,host) from corroborated rows only."""
import json,collections
RUN="20260825T083732Z-b375"; RD=f".specify/3rtask/runs/{RUN}/cycle01"
CUM={"B-PLATFORM","P-PREREQ","H-PLATFORM","H-LIVENESS","X-CONFLICT","N-COMPLIANCE"}
NS=("WP:","ACTOR:","HOST:","BOARD:","UNRESOLVED:")
HOSTS=["ARIELLAS","GAVRI","OLAMNIT","SHIRAS"]
def ns(s):
    s=s.strip(); return s if s.startswith(NS) else "WP:"+s
R=[]
for b in ("builder-1","builder-2","builder-3"):
    d=json.load(open(f"{RD}/claims-{b}.json",encoding="utf-8"))
    for x in (d["claims"] if isinstance(d,dict) else d):
        p=x["claim"].split("|")
        if len(p)<6 or p[0]!="CLAIM": continue
        R.append({"b":b,"s":ns(p[1]),"dim":p[2],"v":p[3],"h":p[4] or "NOHOST","q":p[5] or "QNONE"})

def corrob(dim,single=True):
    """return {key: {value: set(builders)}} where value is verdict or (verdict,qual)"""
    m=collections.defaultdict(lambda: collections.defaultdict(set))
    for r in R:
        if r["dim"]!=dim: continue
        key=(r["s"],r["h"])
        val=r["v"] if single else (r["v"],r["q"])
        m[key][val].add(r["b"])
    return m

loc=corrob("A-LOCALITY")
plat=corrob("B-PLATFORM",single=False)
hostf=corrob("H-PLATFORM",single=False)

packets=sorted({r["s"] for r in R if r["s"].startswith("WP:")})
print(f"packets seen: {len(packets)}")

# corroborated locality per packet
loc_status={}
for p in packets:
    vals=loc.get((p,"NOHOST"),{})
    corr=[v for v,bs in vals.items() if len(bs)>=2 and not v.startswith("UNDECID")]
    if corr: loc_status[p]=corr[0]
    else:
        any_=[v for v in vals if not v.startswith("UNDECID")]
        loc_status[p]="SINGLETON:"+any_[0] if any_ else "NO-CORROBORATED-LOCALITY"
c=collections.Counter(loc_status.values())
print("\n=== locality status per packet (corroborated only counts) ===")
for k,v in c.most_common(): print(f"  {k:34s} {v}")

# corroborated requirement members per packet
req=collections.defaultdict(set); req_c=collections.defaultdict(set)
for (s,h),vals in plat.items():
    for (v,q),bs in vals.items():
        if v=="REQ-DECLARED":
            req[s].add(q)
            if len(bs)>=2: req_c[s].add(q)
print("\n=== packets with a corroborated platform requirement member ===")
n=0
for p in sorted(req_c):
    if req_c[p]: print(f"  {p[:66]:66s} {sorted(req_c[p])}"); n+=1
print(f"  ({n} packets have >=1 CORROBORATED requirement member; {len([x for x in req if req[x]])} have >=1 from any single builder)")

# host facts corroborated present
hf=collections.defaultdict(set)
for (s,h),vals in hostf.items():
    for (v,q),bs in vals.items():
        if v=="HOSTFACT-MEASURED-PRESENT": hf[h].add(q)
print("\n=== corroborated-or-singleton MEASURED-PRESENT host facts ===")
for h in HOSTS: print(f"  {h:9s} {sorted(hf.get(h,[])) or 'NONE MEASURED'}")

print("\n=== L1 DERIVATION: R-RUNNABLE(packet,host) ===")
tot=collections.Counter()
for p in packets:
    ls=loc_status[p]
    for h in HOSTS:
        if ls=="NO-CORROBORATED-LOCALITY" or ls.startswith("SINGLETON"):
            tot[(h,"RUNNABLE-UNDERIVABLE-no-corroborated-locality")]+=1; continue
        if ls.startswith("PINNED-") and ls!="PINNED-"+h:
            tot[(h,"NOT-RUNNABLE-pinned-elsewhere")]+=1; continue
        need=req_c[p]
        if not need:
            tot[(h,"RUNNABLE-UNDERIVABLE-no-corroborated-requirement")]+=1; continue
        unmet=[q for q in need if q not in hf.get(h,set())]
        tot[(h,"RUNNABLE-VERIFIED" if not unmet else "NOT-RUNNABLE-unmet-requirement")]+=1
for h in HOSTS:
    print(f"  {h}:")
    for (hh,v),n in sorted(tot.items()):
        if hh==h: print(f"      {v:56s} {n}")
