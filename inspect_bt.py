import json, re, sys

path = r'd:\github\PopLifeSimulator\Pop Life Simulator\Assets\CustomerBehaviorTree.asset'
content = open(path).read()
m = re.search(r"_serializedGraph: '(.*?)'\n", content, re.DOTALL)
if not m:
    print('NO MATCH')
    sys.exit(1)
js = m.group(1).replace("''", "'")
# YAML embedded JSON may have unescaped newlines; normalize
js = js.replace("\n", " ").replace("\r", " ")
data = json.loads(js)
# Inspect ActionNode 12 (Favorite SetFunnelPhase wrapper) and 19/26/33 (other phases)
for nid in ('12', '19', '26', '33'):
    for n in data.get('nodes', []):
        if n.get('$id') == nid:
            act = n.get('_action', {})
            atype = act.get('$type', '?').split('.')[-1]
            print(f'Node {nid}: action type={atype}')
            if atype == 'ActionList':
                for i, a in enumerate(act.get('actions', [])):
                    t = a.get('$type', '?').split('.')[-1]
                    print(f'  [{i}] {t}')
            else:
                print(f'  (single action, type={atype})')
            break

# build map: source -> [(target, conn_index)]
conns = data.get('connections', [])
adj = {}
for i, c in enumerate(conns):
    src = c.get('_sourceNode', {}).get('$ref')
    tgt = c.get('_targetNode', {}).get('$ref')
    adj.setdefault(src, []).append((tgt, i))

# print nodes of interest with their children in connections order
nodes_of_interest = ['8', '10', '11', '13', '14', '15', '18', '20', '25', '27', '32', '34']
node_types = {n.get('$id'): n.get('$type', '?').split('.')[-1] for n in data.get('nodes', [])}
for nid in nodes_of_interest:
    children = adj.get(nid, [])
    typ = node_types.get(nid, '?')
    print(f'Node {nid} ({typ}) children:')
    for tgt, ci in children:
        ttyp = node_types.get(tgt, '?')
        print(f'  conn[{ci}] -> {tgt} ({ttyp})')

# Check if any node has _isUserDisabled set or has special derivedData
print('\n=== Sequencer 11 raw ===')
for n in data.get('nodes', []):
    if n.get('$id') == '11':
        print(json.dumps(n, indent=2))
        break

print('\n=== derivedData ===')
print(json.dumps(data.get('derivedData', {}), indent=2))
