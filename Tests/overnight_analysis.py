"""Read overnight JSONL/gzip recordings and saves without loading their genomes at once.
Requires ijson; emits summaries to stdout and never changes input files.
"""
import os, sys, gzip, json, statistics, collections
from pathlib import Path
sys.path.insert(0, str(Path(os.environ['TEMP']) / 'tideglass-analysis-deps'))
import ijson

root = Path(os.environ['LOCALAPPDATA']) / 'Tideglass'
recordings = Path(sys.argv[1]) if len(sys.argv) > 1 else root / 'Research'
save = Path(sys.argv[2]) if len(sys.argv) > 2 else root / 'ecosystem.json'
files = sorted(recordings.glob('*.jsonl*'))
headers = []
for path in files:
    try:
        with (gzip.open(path, 'rb') if path.suffix == '.gz' else path.open('rb')) as f:
            h = json.loads(f.readline())
        headers.append((path, h))
    except (EOFError, OSError, ValueError): pass
print(json.dumps({'files':len(files),'bytes':sum(p.stat().st_size for p in files),
    'first':headers[0][1] if headers else None, 'last':headers[-1][1] if headers else None,
    'under_one_simsecond_between_segments':sum(0 <= b[1]['Time']-a[1]['Time'] < 1 for a,b in zip(headers,headers[1:]))}), flush=True)

def summarize(path, saved=False):
    values=collections.defaultdict(list); counts=collections.Counter(); sources=collections.Counter(); meta={}
    with (gzip.open(path,'rb') if path.suffix=='.gz' else path.open('rb')) as f:
        if not saved: meta=json.loads(f.readline())
        prefix='Organisms' if saved else 'organisms'
        builder=None
        for key,event,value in ijson.parse(f, use_float=True, multiple_values=True):
            if key==prefix+'.item' and event=='start_map': builder=ijson.ObjectBuilder()
            if builder is not None: builder.event(event,value)
            if key==prefix+'.item' and event=='end_map':
                o=builder.value; builder=None; g=o.get('Genes',o.get('genes')); counts['population']+=1
                cells=len(o['Body']) if saved else o['cells']
                branches=sum(b['Growth']>.01 for b in o['BudStates']) if saved else o['branches']
                for name,val in [('cells',cells),('expressed_branches',branches),('capacity',o['Capacity']),('age',o['Age']),('generation',o['Generation']),('children',o['Children'])]: values[name].append(val)
                counts['one_cell']+=cells==1; counts['no_branches']+=branches==0; counts['capacity_below_8']+=o['Capacity']<8
                for name in ['LightEnergy','GrazingEnergy','ShoreEnergy','TissueEnergy']: sources[name]+=o[name]
                if g:
                    values['encoded_branches'].append(len(g.get('Buds') or []))
                    values['encoded_cells'].append(g['Cells'])
                    for name in ['Radius','Flattening','Elongation','Photosynthesis','Contract','Sense']: values[name].append(g[name])
                if saved:
                    counts['zero_growth_all']+=all(b['Growth']==0 for b in o['BudStates'])
                    values['branch_growth'].extend(b['Growth'] for b in o['BudStates'])
                    values['wires'].append(sum(len(b['Wires']) for b in g.get('Buds') or []))
            if key==prefix and event=='end_array': break
            if '.' not in key and event in ('number','string','boolean') and key in ['Time','population','Births','Deaths','RecordGeneration','utc']: meta[key]=value
    stats={k:{'mean':round(statistics.mean(v),4),'median':round(statistics.median(v),4),'min':min(v),'max':max(v)} for k,v in values.items() if v}
    total=sum(sources.values()) or 1
    print(json.dumps({'file':path.name,'meta':meta,'counts':dict(counts),'stats':stats,'lifetime_energy_shares':{k:round(v/total,4) for k,v in sources.items()}}),flush=True)

chosen=sorted(set([0, len(headers)//10,len(headers)//3,2*len(headers)//3,max(0,len(headers)-2)])) if headers else []
for i in chosen: summarize(headers[i][0])
if save.exists(): summarize(save, True)
