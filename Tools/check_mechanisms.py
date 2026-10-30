from pathlib import Path
import sys,json
p=Path(sys.argv[1]);m=(p/'Maps.cs').read_text();g=(p/'Game.cs').read_text();u=(p/'GameMobile.cs').read_text()
print(json.dumps({'bounded_streaming':'nearby.Take(12)' in m,'deferred_startup_catalog':'IEnumerator ScanMenuMaps' in g,'safe_area_rebuild':'widthLast=heightLast=-1' in u},sort_keys=True))
