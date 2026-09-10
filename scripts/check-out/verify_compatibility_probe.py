"""Compare a paired synthetic serializer probe; never attribute H2 latency from it."""
import json
from pathlib import Path
import sys


def verify(folder, sha):
    root=Path(folder)
    before=json.loads((root/'before.json').read_text())
    after=json.loads((root/'after.json').read_text())
    for report,expected in ((before,'2742459e9e26b1153c438d7c623778e7c8364957'),(after,sha)):
        if report.get('schema')!='nera.xlsx.compatibility.probe.v1' or report.get('sourceSha')!=expected:
            raise ValueError('Probe source mismatch')
        if report['cells']!=100000 or report['worksheets']!=5 or report['conditionalRules']!=29 or report['measuredIterations']!=3:
            raise ValueError('Different benchmark workload')
        if not report['syntheticFixture'] or report['measuresH2Integration'] or report['measuresScrolling']:
            raise ValueError('Unsupported benchmark claim')
        if report['medianLoadMilliseconds']<=0 or report['medianSaveMilliseconds']<=0:raise ValueError('Missing measurements')
    if before['runtime']!=after['runtime'] or before['os']!=after['os']:raise ValueError('Unpaired runtime')
    summary=dict(schema='nera.xlsx.compatibility.paired.v1',sourceSha=sha,baselineSha=before['sourceSha'],
        loadRatio=after['medianLoadMilliseconds']/before['medianLoadMilliseconds'],
        saveRatio=after['medianSaveMilliseconds']/before['medianSaveMilliseconds'],
        allocationRatio=after['medianAllocatedBytes']/before['medianAllocatedBytes'],
        before=before,after=after,claimsH2RootCause=False)
    (root/'comparison.json').write_text(json.dumps(summary,indent=2)+'\n')
    print(json.dumps(summary))


if __name__=='__main__':verify(sys.argv[1],sys.argv[2])
