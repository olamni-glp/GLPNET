"""Deterministic key-merge implementing the FROZEN method's algebra (E20/E21/E27).
Not judgment: pure set-ops on (SUBJECT, DIM, HOST) with per-DIM cardinality."""
import json,collections,sys

RUN="20260825T083732Z-b375"
RD=f".specify/3rtask/runs/{RUN}/cycle01"
CUMULATIVE={"B-PLATFORM","P-PREREQ","H-PLATFORM","H-LIVENESS","X-CONFLICT","N-COMPLIANCE"}
NS=("WP:","ACTOR:","HOST:","BOARD:","UNRESOLVED:")

def norm_subject(s):
    s=s.strip()
    for p in NS:
        if s.startswith(p): return s
    # E01 violation repair: a bare board wp id is a WP subject
    return "WP:"+s

def parse(c):
    p=c.split("|")
    if len(p)<6: return None
    shape=p[0]
    if shape!="CLAIM": return None
    return {"subject":norm_subject(p[1]),"dim":p[2],"verdict":p[3],
            "host":p[4] or "NOHOST","qual":p[5] or "QNONE"}

rows=collections.defaultdict(lambda:{"members":collections.defaultdict(set),"builders":set()})
raw=collections.Counter(); repaired=0
for b in ("builder-1","builder-2","builder-3"):
    d=json.load(open(f"{RD}/claims-{b}.json",encoding="utf-8"))
    cl=d["claims"] if isinstance(d,dict) else d
    for x in cl:
        f=parse(x["claim"])
        if not f: raw["unparsed"]+=1; continue
        raw["parsed"]+=1
        if not x["claim"].split("|")[1].startswith(NS): repaired+=1
        k=(f["subject"],f["dim"],f["host"])
        cv=(f["verdict"],f["qual"]) if f["dim"] in CUMULATIVE else (f["verdict"],)
        rows[k]["members"][cv].add(b)
        rows[k]["builders"].add(b)

corr=sing=conf=0; corr_rows=[];conf_rows=[]
for k,v in rows.items():
    dim=k[1]
    real={cv:bs for cv,bs in v["members"].items() if not cv[0].startswith("UNDECIDABLE")}
    if dim in CUMULATIVE:
        # union members; corroboration is per-member
        anymulti=any(len(bs)>=2 for bs in real.values())
        if anymulti: corr+=1; corr_rows.append((k,real))
        elif real: sing+=1
        else: sing+=1
    else:
        vs={cv[0] for cv in real}
        if len(vs)>=2: conf+=1; conf_rows.append((k,real))
        elif len(vs)==1:
            bs=list(real.values())[0]
            if len(bs)>=2: corr+=1; corr_rows.append((k,real))
            else: sing+=1
        else: sing+=1

print(f"claims parsed        : {raw['parsed']}   unparsed: {raw['unparsed']}")
print(f"subjects repaired    : {repaired}  (missing namespace prefix, E01 violation)")
print(f"distinct keys        : {len(rows)}")
print(f"CORROBORATED keys    : {corr}")
print(f"SINGLETON keys       : {sing}")
print(f"CONFLICT keys        : {conf}")
print()
print("=== CONFLICTS (rival answers on one single-valued key) ===")
for k,real in conf_rows:
    print(f"  {k[0]}  [{k[1]}]  host={k[2]}")
    for cv,bs in sorted(real.items()): print(f"      {cv[0]:34s} <- {sorted(bs)}")
print()
print(f"=== CORROBORATED (first 22 of {corr}) ===")
for k,real in corr_rows[:22]:
    best=[(cv,bs) for cv,bs in real.items() if len(bs)>=2]
    for cv,bs in best[:2]:
        print(f"  {k[0][:58]:58s} [{k[1]:12s}] {cv[0][:26]:26s} <- {sorted(bs)}")
json.dump({"corroborated":corr,"singleton":sing,"conflict":conf,
           "conflicts":[{"key":list(k),"values":{cv[0]:sorted(bs) for cv,bs in r.items()}} for k,r in conf_rows],
           "corroborated_rows":[{"key":list(k),"values":{cv[0]:sorted(bs) for cv,bs in r.items() if len(bs)>=2}} for k,r in corr_rows]},
          open(f".specify/3rtask/runs/{RUN}/cycle01/keymerge.json","w",encoding="utf-8"),indent=1)
