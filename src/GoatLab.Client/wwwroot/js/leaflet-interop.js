// GoatLab Leaflet.js Interop — Full farm mapping module
window.leafletInterop = (function () {
    let map = null;
    let drawnItems = null;
    let drawControl = null;
    let markers = {};
    let measureLine = null;
    let measurePoints = [];
    let isMeasuring = false;
    let dotNetRef = null;

    // Tile layers
    const tileLayers = {
        osm: L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; OpenStreetMap contributors',
            maxZoom: 19
        }),
        topo: L.tileLayer('https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; OpenTopoMap',
            maxZoom: 17
        }),
        cartoLight: L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png', {
            attribution: '&copy; CARTO',
            maxZoom: 20
        }),
        cartoDark: L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', {
            attribution: '&copy; CARTO',
            maxZoom: 20
        })
    };

    let currentTileLayer = null;

    // Marker icons by type
    const markerIcons = {
        Barn: createIcon('#8B4513', 'home'),
        Shelter: createIcon('#795548', 'roofing'),
        Water: createIcon('#2196F3', 'water_drop'),
        Feeder: createIcon('#FF9800', 'restaurant'),
        Gate: createIcon('#9C27B0', 'meeting_room'),
        Other: createIcon('#607D8B', 'place')
    };

    function createIcon(color, _iconName) {
        return L.divIcon({
            className: 'custom-map-marker',
            html: `<div style="background:${color};width:28px;height:28px;border-radius:50%;border:3px solid white;box-shadow:0 2px 6px rgba(0,0,0,0.3);display:flex;align-items:center;justify-content:center;">
                     <span style="color:white;font-size:14px;font-weight:bold;">${_iconName[0].toUpperCase()}</span>
                   </div>`,
            iconSize: [28, 28],
            iconAnchor: [14, 14],
            popupAnchor: [0, -16]
        });
    }

    // Condition colors for heat map
    const conditionColors = {
        1: '#d32f2f', // Poor - red
        2: '#f57c00', // Fair - orange
        3: '#fbc02d', // Good - yellow
        4: '#689f38', // Very Good - light green
        5: '#2e7d32'  // Excellent - green
    };

    return {
        // Initialize the map
        init: function (elementId, lat, lng, zoom, dotNetObjRef) {
            if (map) {
                map.remove();
                map = null;
            }

            dotNetRef = dotNetObjRef;
            map = L.map(elementId).setView([lat, lng], zoom);

            // Default to OSM
            currentTileLayer = tileLayers.osm;
            currentTileLayer.addTo(map);

            // Drawn items layer
            drawnItems = new L.FeatureGroup();
            map.addLayer(drawnItems);

            // Draw controls
            drawControl = new L.Control.Draw({
                position: 'topright',
                draw: {
                    polygon: {
                        allowIntersection: false,
                        showArea: true,
                        shapeOptions: { color: '#2e7d32', weight: 2, fillOpacity: 0.15 }
                    },
                    polyline: {
                        shapeOptions: { color: '#1976d2', weight: 3, dashArray: '10, 5' }
                    },
                    rectangle: {
                        shapeOptions: { color: '#f57c00', weight: 2, fillOpacity: 0.15 }
                    },
                    circle: false,
                    circlemarker: false,
                    marker: true
                },
                edit: {
                    featureGroup: drawnItems,
                    remove: true
                }
            });
            map.addControl(drawControl);

            // Handle drawn shapes
            map.on(L.Draw.Event.CREATED, function (event) {
                const layer = event.layer;
                const type = event.layerType;
                drawnItems.addLayer(layer);

                if (type === 'polygon' || type === 'rectangle') {
                    const geoJson = JSON.stringify(layer.toGeoJSON());
                    const area = calculateArea(layer);
                    const perimeter = calculatePerimeter(layer);

                    layer.bindPopup(
                        `<strong>Area:</strong> ${area.acres.toFixed(2)} acres (${area.sqft.toFixed(0)} sq ft)<br>` +
                        `<strong>Perimeter:</strong> ${perimeter.toFixed(0)} ft`
                    ).openPopup();

                    if (dotNetRef) {
                        dotNetRef.invokeMethodAsync('OnShapeDrawn', geoJson, area.acres, perimeter);
                    }
                } else if (type === 'polyline') {
                    const distance = calculateLineDistance(layer);
                    layer.bindPopup(`<strong>Distance:</strong> ${distance.feet.toFixed(0)} ft (${distance.miles.toFixed(2)} mi)`).openPopup();
                } else if (type === 'marker') {
                    const latlng = layer.getLatLng();
                    if (dotNetRef) {
                        dotNetRef.invokeMethodAsync('OnMarkerPlaced', latlng.lat, latlng.lng);
                    }
                }
            });

            // Scale control
            L.control.scale({ imperial: true, metric: true }).addTo(map);

            return true;
        },

        // Switch tile layer
        setTileLayer: function (layerName, googleApiKey) {
            if (!map) return;

            if (currentTileLayer) {
                map.removeLayer(currentTileLayer);
            }

            if (layerName === 'satellite' && googleApiKey) {
                currentTileLayer = L.tileLayer(
                    `https://mt1.google.com/vt/lyrs=s&x={x}&y={y}&z={z}&key=${googleApiKey}`, {
                    attribution: '&copy; Google',
                    maxZoom: 20
                });
            } else if (tileLayers[layerName]) {
                currentTileLayer = tileLayers[layerName];
            } else {
                currentTileLayer = tileLayers.osm;
            }

            currentTileLayer.addTo(map);
        },

        // Add a marker
        addMarker: function (id, lat, lng, name, type, description) {
            if (!map) return;

            const icon = markerIcons[type] || markerIcons.Other;
            const marker = L.marker([lat, lng], { icon: icon })
                .bindPopup(`<strong>${name}</strong><br><em>${type}</em><br>${description || ''}`)
                .addTo(map);

            markers[id] = marker;
        },

        // Remove a marker
        removeMarker: function (id) {
            if (markers[id]) {
                map.removeLayer(markers[id]);
                delete markers[id];
            }
        },

        // Add a water source with 100m radius circle
        addWaterRadius: function (lat, lng, name) {
            if (!map) return;

            L.circle([lat, lng], {
                radius: 100,
                color: '#2196F3',
                fillColor: '#2196F3',
                fillOpacity: 0.08,
                weight: 1,
                dashArray: '5, 5'
            }).bindPopup(`<strong>${name}</strong><br>100m water radius`).addTo(map);
        },

        // Load pasture polygon from GeoJSON
        addPasturePolygon: function (id, geoJsonStr, name, condition, acreage) {
            if (!map) return;

            try {
                const geoJson = JSON.parse(geoJsonStr);
                const color = conditionColors[condition] || '#2e7d32';

                const layer = L.geoJSON(geoJson, {
                    style: {
                        color: color,
                        weight: 2,
                        fillColor: color,
                        fillOpacity: 0.2
                    }
                }).bindPopup(
                    `<strong>${name}</strong><br>` +
                    `Condition: ${condition}/5<br>` +
                    `Acreage: ${acreage ? acreage.toFixed(2) : '?'} ac`
                ).addTo(map);

                drawnItems.addLayer(layer);
            } catch (e) {
                console.warn('Failed to parse GeoJSON for pasture', id, e);
            }
        },

        // Toggle measurement mode
        toggleMeasure: function () {
            isMeasuring = !isMeasuring;
            measurePoints = [];

            if (measureLine) {
                map.removeLayer(measureLine);
                measureLine = null;
            }

            if (isMeasuring) {
                map.getContainer().style.cursor = 'crosshair';
                map.on('click', onMeasureClick);
            } else {
                map.getContainer().style.cursor = '';
                map.off('click', onMeasureClick);
            }

            return isMeasuring;
        },

        // Fit map to show all drawn items
        fitBounds: function () {
            if (!map || !drawnItems) return;
            const bounds = drawnItems.getBounds();
            if (bounds.isValid()) {
                map.fitBounds(bounds, { padding: [30, 30] });
            }
        },

        // Set map center
        setView: function (lat, lng, zoom) {
            if (map) map.setView([lat, lng], zoom);
        },

        // Export drawn items as GeoJSON
        exportGeoJson: function () {
            if (!drawnItems) return null;
            return JSON.stringify(drawnItems.toGeoJSON());
        },

        // Clear all drawn items
        clearDrawn: function () {
            if (drawnItems) drawnItems.clearLayers();
        },

        // Destroy the map
        dispose: function () {
            if (map) {
                map.remove();
                map = null;
                drawnItems = null;
                markers = {};
            }
        }
    };

    // --- Helper functions ---

    function onMeasureClick(e) {
        measurePoints.push(e.latlng);

        if (measureLine) {
            map.removeLayer(measureLine);
        }

        if (measurePoints.length >= 2) {
            measureLine = L.polyline(measurePoints, {
                color: '#e91e63',
                weight: 3,
                dashArray: '8, 8'
            }).addTo(map);

            let totalMeters = 0;
            for (let i = 1; i < measurePoints.length; i++) {
                totalMeters += measurePoints[i - 1].distanceTo(measurePoints[i]);
            }
            const feet = totalMeters * 3.28084;
            const miles = feet / 5280;

            measureLine.bindPopup(
                `<strong>Distance:</strong> ${feet.toFixed(0)} ft (${miles.toFixed(2)} mi)`
            ).openPopup();
        }
    }

    function calculateArea(layer) {
        const latlngs = layer.getLatLngs()[0];
        // Shoelace formula on projected coordinates
        let areaM2 = 0;
        const projected = latlngs.map(ll => L.Projection.SphericalMercator.project(ll));
        for (let i = 0; i < projected.length; i++) {
            const j = (i + 1) % projected.length;
            areaM2 += projected[i].x * projected[j].y;
            areaM2 -= projected[j].x * projected[i].y;
        }
        areaM2 = Math.abs(areaM2) / 2;
        const sqft = areaM2 * 10.7639;
        const acres = sqft / 43560;
        return { sqft, acres, sqMeters: areaM2 };
    }

    function calculatePerimeter(layer) {
        const latlngs = layer.getLatLngs()[0];
        let perimeterM = 0;
        for (let i = 0; i < latlngs.length; i++) {
            const j = (i + 1) % latlngs.length;
            perimeterM += latlngs[i].distanceTo(latlngs[j]);
        }
        return perimeterM * 3.28084; // feet
    }

    function calculateLineDistance(layer) {
        const latlngs = layer.getLatLngs();
        let totalMeters = 0;
        for (let i = 1; i < latlngs.length; i++) {
            totalMeters += latlngs[i - 1].distanceTo(latlngs[i]);
        }
        return { meters: totalMeters, feet: totalMeters * 3.28084, miles: (totalMeters * 3.28084) / 5280 };
    }
})();
