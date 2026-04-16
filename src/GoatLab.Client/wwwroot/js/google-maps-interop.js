// GoatLab Google Maps Interop — satellite-first farm mapping module
window.goatMaps = (function () {
    let map = null;
    let dotNetRef = null;

    let markers = {};              // markerId -> { marker, infoWindow, type }
    let waterCircles = {};         // markerId -> circle
    let pasturePolygons = {};      // pastureId -> { polygon, infoWindow }
    let barnMarkers = {};          // barnId -> { marker, infoWindow }
    let drawingManager = null;

    // Layer visibility
    const visible = { pastures: true, markers: true, barns: true, water: true };

    let measureMode = false;
    let measurePoly = null;
    let measurePoints = [];
    let measureMarkers = [];

    const conditionColors = {
        1: '#d32f2f', 2: '#f57c00', 3: '#fbc02d', 4: '#689f38', 5: '#2e7d32'
    };

    const markerStyles = {
        Barn:    { color: '#8B4513' },
        Shelter: { color: '#795548' },
        Water:   { color: '#2196F3' },
        Feeder:  { color: '#FF9800' },
        Gate:    { color: '#9C27B0' },
        Other:   { color: '#607D8B' }
    };

    function loadScript(apiKey) {
        return new Promise((resolve, reject) => {
            if (window.google && window.google.maps) { resolve(); return; }
            if (document.getElementById('goat-google-maps-js')) {
                const check = setInterval(() => {
                    if (window.google && window.google.maps) { clearInterval(check); resolve(); }
                }, 50);
                return;
            }
            const s = document.createElement('script');
            s.id = 'goat-google-maps-js';
            s.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(apiKey)}&libraries=geometry,drawing&v=weekly`;
            s.async = true;
            s.defer = true;
            s.onload = () => resolve();
            s.onerror = () => reject(new Error('Failed to load Google Maps JS'));
            document.head.appendChild(s);
        });
    }

    function buildMarkerIcon(type) {
        const s = markerStyles[type] || markerStyles.Other;
        return {
            path: google.maps.SymbolPath.CIRCLE,
            scale: 11,
            fillColor: s.color,
            fillOpacity: 1,
            strokeColor: '#fff',
            strokeWeight: 3
        };
    }

    function buildBarnIcon() {
        return {
            path: 'M -14 0 L 0 -14 L 14 0 L 14 12 L -14 12 Z',
            scale: 1,
            fillColor: '#6d4c41',
            fillOpacity: 1,
            strokeColor: '#fff',
            strokeWeight: 2,
            anchor: new google.maps.Point(0, 0)
        };
    }

    function popupHtml(title, lines, extra) {
        const rows = lines.filter(Boolean).map(l => `<div style="color:#555;font-size:0.85rem;">${l}</div>`).join('');
        return `<div style="min-width:200px;font-family:Inter,sans-serif;">
                    <div style="font-weight:700;color:#1b5e20;margin-bottom:4px;">${title}</div>
                    ${rows}
                    ${extra || ''}
                </div>`;
    }

    function onPolygonComplete(polygon) {
        const path = polygon.getPath();
        const pathArray = [];
        for (let i = 0; i < path.getLength(); i++) {
            const p = path.getAt(i);
            pathArray.push({ lat: p.lat(), lng: p.lng() });
        }
        const sqMeters = google.maps.geometry.spherical.computeArea(path);
        const acres = sqMeters / 4046.8564224;
        const perimeterM = google.maps.geometry.spherical.computeLength(
            path.getArray().concat([path.getAt(0)])
        );
        const perimeterFt = perimeterM * 3.28084;

        const coords = pathArray.map(p => [p.lng, p.lat]);
        coords.push([pathArray[0].lng, pathArray[0].lat]);
        const geoJson = JSON.stringify({
            type: 'Feature',
            geometry: { type: 'Polygon', coordinates: [coords] },
            properties: {}
        });

        const iw = new google.maps.InfoWindow({
            content: popupHtml('New area', [
                `<strong>${acres.toFixed(2)} acres</strong> (${(sqMeters * 10.7639).toFixed(0)} sq ft)`,
                `Perimeter: <strong>${perimeterFt.toFixed(0)} ft</strong>`
            ])
        });
        iw.setPosition(pathArray[0]);
        iw.open(map);
        polygon.setEditable(false);
        if (dotNetRef) dotNetRef.invokeMethodAsync('OnShapeDrawn', geoJson, acres, perimeterFt);
        drawingManager.setDrawingMode(null);
    }

    function onMarkerComplete(marker) {
        const pos = marker.getPosition();
        marker.setMap(null);
        drawingManager.setDrawingMode(null);
        if (dotNetRef) dotNetRef.invokeMethodAsync('OnMarkerPlaced', pos.lat(), pos.lng());
    }

    function clearMeasure() {
        if (measurePoly) { measurePoly.setMap(null); measurePoly = null; }
        measureMarkers.forEach(m => m.setMap(null));
        measureMarkers = [];
        measurePoints = [];
    }

    function onMeasureClick(e) {
        measurePoints.push(e.latLng);
        const m = new google.maps.Marker({
            position: e.latLng, map,
            icon: { path: google.maps.SymbolPath.CIRCLE, scale: 5, fillColor: '#e91e63', fillOpacity: 1, strokeWeight: 2, strokeColor: '#fff' }
        });
        measureMarkers.push(m);
        if (measurePoly) measurePoly.setMap(null);
        if (measurePoints.length >= 2) {
            measurePoly = new google.maps.Polyline({
                path: measurePoints, map,
                strokeColor: '#e91e63', strokeWeight: 3,
                icons: [{ icon: { path: 'M 0,-1 0,1', strokeOpacity: 1, scale: 3 }, offset: '0', repeat: '10px' }]
            });
            const meters = google.maps.geometry.spherical.computeLength(measurePoints);
            const feet = meters * 3.28084;
            const miles = feet / 5280;
            const iw = new google.maps.InfoWindow({
                content: popupHtml('Distance', [`<strong>${feet.toFixed(0)} ft</strong> (${miles.toFixed(2)} mi)`])
            });
            iw.setPosition(measurePoints[measurePoints.length - 1]);
            iw.open(map);
        }
    }

    return {
        init: async function (elementId, apiKey, lat, lng, zoom, dotNetObjRef) {
            dotNetRef = dotNetObjRef;
            await loadScript(apiKey);

            map = new google.maps.Map(document.getElementById(elementId), {
                center: { lat, lng },
                zoom,
                mapTypeId: 'hybrid',
                mapTypeControl: true,
                mapTypeControlOptions: {
                    style: google.maps.MapTypeControlStyle.HORIZONTAL_BAR,
                    mapTypeIds: ['roadmap', 'satellite', 'hybrid', 'terrain']
                },
                streetViewControl: false,
                fullscreenControl: true,
                zoomControl: true,
                tilt: 0
            });

            drawingManager = new google.maps.drawing.DrawingManager({
                drawingMode: null,
                drawingControl: true,
                drawingControlOptions: {
                    position: google.maps.ControlPosition.TOP_RIGHT,
                    drawingModes: ['marker', 'polygon', 'polyline', 'rectangle']
                },
                polygonOptions: { strokeColor: '#2e7d32', strokeWeight: 2, fillColor: '#2e7d32', fillOpacity: 0.2, editable: true },
                polylineOptions: { strokeColor: '#1976d2', strokeWeight: 3 },
                rectangleOptions: { strokeColor: '#f57c00', strokeWeight: 2, fillColor: '#f57c00', fillOpacity: 0.2, editable: true }
            });
            drawingManager.setMap(map);

            google.maps.event.addListener(drawingManager, 'polygoncomplete', onPolygonComplete);
            google.maps.event.addListener(drawingManager, 'rectanglecomplete', (rect) => {
                const b = rect.getBounds();
                const ne = b.getNorthEast(), sw = b.getSouthWest();
                const corners = [
                    { lat: ne.lat(), lng: sw.lng() },
                    { lat: ne.lat(), lng: ne.lng() },
                    { lat: sw.lat(), lng: ne.lng() },
                    { lat: sw.lat(), lng: sw.lng() }
                ];
                rect.setMap(null);
                const poly = new google.maps.Polygon({
                    paths: corners, map,
                    strokeColor: '#2e7d32', strokeWeight: 2, fillColor: '#2e7d32', fillOpacity: 0.2, editable: true
                });
                onPolygonComplete(poly);
            });
            google.maps.event.addListener(drawingManager, 'markercomplete', onMarkerComplete);

            return true;
        },

        setMapType: function (type) { if (map) map.setMapTypeId(type); },

        // --- Markers (generic) ---
        addMarker: function (id, lat, lng, name, type, description) {
            if (!map) return;
            const marker = new google.maps.Marker({
                position: { lat, lng },
                map: visible.markers && (type !== 'Water' || visible.water) ? map : null,
                title: name,
                icon: buildMarkerIcon(type),
                draggable: true
            });
            const iw = new google.maps.InfoWindow({
                content: popupHtml(name, [`<em>${type}</em>`, description || ''])
            });
            marker.addListener('click', () => iw.open(map, marker));
            marker.addListener('dragend', (e) => {
                if (dotNetRef) dotNetRef.invokeMethodAsync('OnMarkerDragged', id, e.latLng.lat(), e.latLng.lng());
            });
            markers[id] = { marker, iw, type };
        },

        removeMarker: function (id) {
            if (markers[id]) { markers[id].marker.setMap(null); delete markers[id]; }
            if (waterCircles[id]) { waterCircles[id].setMap(null); delete waterCircles[id]; }
        },

        addWaterRadius: function (id, lat, lng, name, radiusM) {
            if (!map) return;
            const circle = new google.maps.Circle({
                map: visible.water && visible.markers ? map : null,
                center: { lat, lng },
                radius: radiusM || 100,
                strokeColor: '#2196F3', strokeWeight: 1, strokeOpacity: 0.8,
                fillColor: '#2196F3', fillOpacity: 0.08
            });
            waterCircles[id] = circle;
        },

        // --- Barns ---
        addBarn: function (id, lat, lng, name, pens) {
            if (!map) return;
            if (barnMarkers[id]) { barnMarkers[id].marker.setMap(null); delete barnMarkers[id]; }

            const marker = new google.maps.Marker({
                position: { lat, lng },
                map: visible.barns ? map : null,
                title: name,
                icon: buildBarnIcon(),
                label: { text: '🏠', fontSize: '14px' },
                draggable: true
            });

            let penLines = '<div style="font-weight:600;margin-top:8px;color:#333;">Pens</div>';
            if (pens && pens.length > 0) {
                penLines += pens.map(p => {
                    const count = p.goatCount || 0;
                    const capStr = p.capacity > 0 ? ` / ${p.capacity}` : '';
                    const goats = (p.goats || []).slice(0, 5)
                        .map(g => `<a href="/herd/${g.id}" style="color:#2e7d32;text-decoration:none;">${g.name}</a>`)
                        .join(', ');
                    const more = (p.goats || []).length > 5 ? `, +${p.goats.length - 5} more` : '';
                    return `<div style="display:flex;justify-content:space-between;padding:4px 0;border-bottom:1px solid #eee;">
                                <div><strong>${p.name}</strong> <span style="color:#888;font-size:0.8rem;">${count}${capStr}</span></div>
                            </div>
                            ${goats ? `<div style="font-size:0.8rem;color:#666;padding-left:8px;padding-bottom:6px;">${goats}${more}</div>` : ''}`;
                }).join('');
            } else {
                penLines += '<div style="color:#999;font-size:0.85rem;padding:4px 0;">No pens defined.</div>';
            }

            const iw = new google.maps.InfoWindow({
                content: popupHtml(`🏠 ${name}`, [`<em>Barn</em>`], penLines)
            });
            marker.addListener('click', () => iw.open(map, marker));
            marker.addListener('dragend', (e) => {
                if (dotNetRef) dotNetRef.invokeMethodAsync('OnBarnDragged', id, e.latLng.lat(), e.latLng.lng());
            });
            barnMarkers[id] = { marker, iw };
        },

        removeBarn: function (id) {
            if (barnMarkers[id]) { barnMarkers[id].marker.setMap(null); delete barnMarkers[id]; }
        },

        // --- Pasture polygons ---
        addPasturePolygon: function (id, geoJsonStr, name, condition, acreage, isActiveRotation, daysSinceGrazed) {
            if (!map) return;
            try {
                const gj = JSON.parse(geoJsonStr);
                const geom = gj.geometry || gj;
                if (!geom || geom.type !== 'Polygon') return;
                const ring = geom.coordinates[0];
                const paths = ring.map(c => ({ lat: c[1], lng: c[0] }));
                const color = conditionColors[condition] || '#2e7d32';
                const opacity = isActiveRotation ? 0.5 : 0.22;
                const weight = isActiveRotation ? 4 : 2;

                const poly = new google.maps.Polygon({
                    paths,
                    map: visible.pastures ? map : null,
                    strokeColor: color,
                    strokeWeight: weight,
                    fillColor: color,
                    fillOpacity: opacity,
                    zIndex: isActiveRotation ? 10 : 1
                });
                const daysLine = daysSinceGrazed != null
                    ? (daysSinceGrazed < 0 ? 'Currently grazing' : `${daysSinceGrazed} day${daysSinceGrazed === 1 ? '' : 's'} since grazed`)
                    : 'Never grazed on record';
                const iw = new google.maps.InfoWindow({
                    content: popupHtml(name + (isActiveRotation ? ' 🐐' : ''), [
                        `Condition: <strong>${condition}/5</strong>`,
                        `Acreage: <strong>${acreage != null ? acreage.toFixed(2) : '?'} ac</strong>`,
                        daysLine
                    ])
                });
                poly.addListener('click', (e) => { iw.setPosition(e.latLng); iw.open(map); });
                pasturePolygons[id] = { polygon: poly, iw };
            } catch (e) {
                console.warn('Failed to render pasture polygon', id, e);
            }
        },

        removePasturePolygon: function (id) {
            if (pasturePolygons[id]) {
                pasturePolygons[id].polygon.setMap(null);
                delete pasturePolygons[id];
            }
        },

        // --- Layer toggles ---
        setLayerVisibility: function (layer, isVisible) {
            visible[layer] = !!isVisible;
            const tgt = isVisible ? map : null;
            if (layer === 'pastures') {
                Object.values(pasturePolygons).forEach(({ polygon }) => polygon.setMap(tgt));
            } else if (layer === 'markers') {
                Object.values(markers).forEach(({ marker }) => marker.setMap(tgt));
                Object.values(waterCircles).forEach(c => c.setMap(visible.water && isVisible ? map : null));
            } else if (layer === 'water') {
                Object.values(waterCircles).forEach(c => c.setMap(isVisible && visible.markers ? map : null));
            } else if (layer === 'barns') {
                Object.values(barnMarkers).forEach(({ marker }) => marker.setMap(tgt));
            }
        },

        // --- Measure ---
        toggleMeasure: function () {
            measureMode = !measureMode;
            if (measureMode) {
                clearMeasure();
                map.setOptions({ draggableCursor: 'crosshair' });
                this._measureListener = map.addListener('click', onMeasureClick);
            } else {
                map.setOptions({ draggableCursor: null });
                if (this._measureListener) { google.maps.event.removeListener(this._measureListener); this._measureListener = null; }
                clearMeasure();
            }
            return measureMode;
        },

        fitBounds: function () {
            if (!map) return;
            const bounds = new google.maps.LatLngBounds();
            let any = false;
            Object.values(markers).forEach(({ marker }) => { bounds.extend(marker.getPosition()); any = true; });
            Object.values(barnMarkers).forEach(({ marker }) => { bounds.extend(marker.getPosition()); any = true; });
            Object.values(pasturePolygons).forEach(({ polygon }) => {
                polygon.getPath().forEach(p => { bounds.extend(p); any = true; });
            });
            if (any) map.fitBounds(bounds, 40);
        },

        setView: function (lat, lng, zoom) {
            if (!map) return;
            map.setCenter({ lat, lng });
            if (zoom != null) map.setZoom(zoom);
        },

        clearDrawn: function () { },

        dispose: function () {
            clearMeasure();
            Object.values(markers).forEach(({ marker }) => marker.setMap(null));
            Object.values(waterCircles).forEach(c => c.setMap(null));
            Object.values(pasturePolygons).forEach(({ polygon }) => polygon.setMap(null));
            Object.values(barnMarkers).forEach(({ marker }) => marker.setMap(null));
            markers = {}; waterCircles = {}; pasturePolygons = {}; barnMarkers = {};
            if (drawingManager) { drawingManager.setMap(null); drawingManager = null; }
            map = null;
        }
    };
})();
