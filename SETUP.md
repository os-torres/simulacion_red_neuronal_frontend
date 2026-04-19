# Configuración del Proyecto Unity
## Clasificador de Redes Neuronales

> La escena se construye **automáticamente** con un script wizard.  
> Solo tienes que seguir estos pasos en orden.

---

## Paso 1 — Inicia el backend Python

Abre una terminal y ejecuta:

```bash
cd simulacion_red_neuronal_backend
pip install -r requirements.txt
uvicorn main:app --host 0.0.0.0 --port 8000 --reload
```

Verifica en el navegador: **http://localhost:8000/health**  
Debe aparecer: `{"status":"ok","message":"API LISTA Y CONECTADA"}`

Deja esa terminal abierta.

---

## Paso 2 — Abre el proyecto en Unity

1. Abre **Unity Hub**
2. Clic en **"Add project from disk"**
3. Selecciona la carpeta `simulacion_red_neuronal_frontend`
4. Espera 2-3 minutos a que compile

---

## Paso 3 — Activa el nuevo Input System

1. Menú: **Edit → Project Settings → Player → Other Settings**
2. Busca **"Active Input Handling"**
3. Cámbialo a **"Input System Package (New)"**
4. Unity pregunta si reiniciar → **"Yes"**
5. Espera que reabre

---

## Paso 4 — Crea una nueva escena

1. Menú: **File → New Scene → Basic (URP)** → clic **Create**
2. Guarda: **File → Save As...** → carpeta `Assets/Scenes/` → nombre `Main` → **Save**

---

## Paso 5 — Importa TMP Essentials (si no está)

1. Menú: **Window → TextMeshPro → Import TMP Essential Resources**
2. En la ventana que aparece, clic **"Import"**
3. Espera que termine

---

## Paso 6 — Ejecuta el Wizard (CONSTRUYE TODO AUTOMÁTICAMENTE)

1. Menú superior: **Tools → ⚙️ Configurar Escena Completa**
2. Aparece un diálogo de confirmación → clic **"Sí, construir escena"**
3. Una barra de progreso muestra lo que está haciendo (~10 segundos)
4. Al terminar aparece un diálogo de éxito con instrucciones finales

**El wizard crea automáticamente:**
- Todos los GameObjects (cámaras, paneles, botones, sliders, toggles...)
- Todos los componentes con sus valores configurados
- Los 3 prefabs necesarios (InstRow, SepRow, ResultRow)
- El RenderTexture para la vista 3D (RT_3D)
- La capa "Visualization" para aislar la cámara 3D
- Todas las conexiones entre scripts y referencias del Inspector
- Todos los eventos OnClick de los botones

---

## Paso 7 — Guarda la escena

**File → Save** (o Ctrl+S)

---

## Paso 8 — Presiona Play ▶

1. Verifica que el backend Python sigue corriendo
2. Presiona el botón **▶ Play** en Unity
3. La StatusBar debe mostrar **"CONECTANDO..."** y luego **"CONECTADO — API disponible"**

---

## Flujo de uso

```
1. Panel izquierdo → configura los datos → "GENERAR DATOS"
2. Panel izquierdo → configura la red → "ENTRENAR"
3. StatusBar muestra el progreso en tiempo real
4. Al terminar: ver gráficas, vista 3D, clasificar puntos, exportar
```

---

## Si algo sale mal

| Problema | Solución |
|---|---|
| No aparece `Tools → ⚙️ Configurar Escena` | Unity no compiló aún — espera unos segundos |
| Error "TMP Input Field prefab no encontrado" | Repite el Paso 5 (Import TMP Essentials) y vuelve a ejecutar el wizard |
| StatusBar siempre en "CONECTANDO..." | El backend Python no está corriendo (Paso 1) |
| Errores rojos en la consola de Unity | Lee el mensaje — generalmente es un campo no asignado; el wizard lo indica |
| La vista 3D está en negro | Normal hasta que entrenas y pulsas el tab "VISTA 3D" |
| Input Manager warning | Repite el Paso 3 |
