"""Repite contratos y resultados ya verificados, ahora contra una API conectada a Atlas."""
import argparse
import json
from decimal import Decimal
from datetime import datetime, timezone
from pathlib import Path
from urllib.request import urlopen
from urllib.error import HTTPError

parser = argparse.ArgumentParser()
parser.add_argument('--url', default='http://127.0.0.1:5089')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
baseline = json.loads((root / 'evidencias/integracion-http.json').read_text(encoding='utf-8-sig'), parse_float=Decimal)
results = []
for case in baseline['solicitudes']:
    route = case['ruta']
    try:
        response = urlopen(args.url.rstrip('/') + route, timeout=30)
    except HTTPError as error:
        response = error
    raw = response.read().decode('utf-8')
    actual = json.loads(raw, parse_float=Decimal)
    assert response.status == case['codigo'], f'Código incorrecto en {route}: {response.status}'
    assert 'json' in response.headers.get('Content-Type', ''), route
    expected = case['respuesta']
    if route == '/health/ready':
        assert actual['origen'] == 'MongoDB Atlas', 'La prueba requiere Atlas real'
    if 'datos' in expected:
        assert actual['meta']['origen'] == 'MongoDB Atlas', route
        if route.endswith('?Moneda=USD'):
            # La venta centinela USD pertenece únicamente a la base temporal local.
            assert actual['datos'] == dict(ventas=0, facturacion=0, ingresosNetos=0, impuestos=0, ticketPromedio=0)
        else:
            assert actual['datos'] == expected['datos'], f'Resultado distinto al oráculo local: {route}'
    elif route == '/openapi/v1.json':
        assert set(actual['paths']) == set(expected['paths'])
    elif response.status >= 400:
        assert actual['title'] == expected['title']
    results.append(dict(ruta=route, codigo=response.status, respuesta=json.loads(raw), coincide=True))
out = root / 'evidencias/atlas'
out.mkdir(exist_ok=True)
(out / 'verificacion.json').write_text(json.dumps(dict(
    ejecutadoUtc=datetime.now(timezone.utc).isoformat(), origen='MongoDB Atlas',
    resultado='Todos los contratos y resultados comparados correctamente',
    solicitudes=results, correctas=len(results)), ensure_ascii=False, indent=2), encoding='utf-8')
print(f'Atlas: {len(results)} solicitudes verificadas contra resultados esperados; evidencia guardada.')
