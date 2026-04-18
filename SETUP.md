# Guía Paso a Paso — Configurar el Proyecto Unity
## Clasificador de Redes Neuronales

> Esta guía asume que **nunca has usado Unity antes**.  
> Cada paso explica qué es cada cosa y por qué lo haces.

---

# PARTE 0 — Antes de abrir Unity

## 0.1 Inicia el backend Python

Abre una terminal y ejecuta:

```bash
cd simulacion_red_neuronal_backend
pip install -r requirements.txt
uvicorn main:app --host 0.0.0.0 --port 8000 --reload
```

Verifica que funciona abriendo en el navegador: **http://localhost:8000/health**  
Debe aparecer: `{"status":"ok","message":"API LISTA Y CONECTADA"}`

Deja esa terminal abierta mientras trabajas en Unity.

---

# PARTE 1 — Abrir el proyecto en Unity

## 1.1 Qué es Unity Hub

Unity Hub es el "lanzador" de Unity. Desde ahí gestionas proyectos y versiones.

## 1.2 Abrir el proyecto

1. Abre **Unity Hub**
2. Haz clic en **"Add project from disk"** (o "Agregar proyecto desde disco")
3. Navega hasta la carpeta `simulacion_red_neuronal_frontend` y selecciónala
4. Unity la agrega a la lista — haz doble clic para abrirla
5. **Espera 2-3 minutos** — Unity está compilando los scripts y descargando paquetes

## 1.3 Qué verás al abrir

Unity tiene varias ventanas:
- **Scene** (centro-izquierda): el espacio visual donde colocas objetos
- **Hierarchy** (izquierda): lista de todos los objetos en la escena actual
- **Inspector** (derecha): propiedades del objeto seleccionado
- **Project** (abajo): archivos de tu proyecto (scripts, imágenes, etc.)
- **Console** (abajo): mensajes de error o debug

---

# PARTE 2 — Configuración inicial obligatoria

## 2.1 Cambiar el Input System (OBLIGATORIO)

El script de la cámara 3D usa el "nuevo Input System". Si no lo activas, habrá errores.

1. En el menú superior ve a **Edit → Project Settings**
2. Se abre una ventana. En el panel izquierdo busca **"Player"**
3. En la sección **"Other Settings"** busca **"Active Input Handling"**
4. Cámbialo a **"Input System Package (New)"**
5. Unity te preguntará si quieres reiniciar — haz clic en **"Yes"**
6. Unity se cierra y vuelve a abrir. Esto es normal.

## 2.2 Verificar URP (Universal Render Pipeline)

URP es el sistema de gráficos del proyecto.

1. Ve a **Edit → Project Settings → Graphics**
2. Si el campo **"Scriptable Render Pipeline Settings"** está vacío:
   - Haz clic en el círculo pequeño a la derecha del campo
   - Busca cualquier asset que diga "URP" o "Universal Render Pipeline"
   - Selecciónalo
3. Si ya tiene un asset asignado, no hagas nada.

---

# PARTE 3 — Crear la escena principal

## 3.1 Qué es una escena

Una "escena" en Unity es como una pantalla de tu aplicación. Todos los objetos visuales viven dentro de una escena.

## 3.2 Crear la escena

1. En el menú superior: **File → New Scene**
2. Elige **"Basic (URP)"** y haz clic en **Create**
3. Guárdala: **File → Save As...**
4. Navega a `Assets/Scenes/` (si la carpeta no existe, créala)
5. Nombra el archivo `Main` y haz clic en **Save**

Verás en la Hierarchy que ya hay dos objetos: `Main Camera` y `Directional Light`. Estos son normales.

---

# PARTE 4 — Entender la Hierarchy

## 4.1 Qué es un GameObject

En Unity, **todo** es un "GameObject". Una cámara, un botón, un panel, un script invisible — todos son GameObjects. Los GameObjects pueden tener "componentes" (pedazos de funcionalidad).

## 4.2 Cómo crear un GameObject vacío

Un GameObject vacío sirve para organizar otros objetos o para ejecutar scripts sin mostrar nada visual.

Para crear uno:
- Clic derecho en la Hierarchy → **"Create Empty"**
- O en el menú: **GameObject → Create Empty**

## 4.3 Cómo renombrar un objeto

- Haz clic una vez en el objeto en la Hierarchy para seleccionarlo
- Luego haz clic de nuevo (más lento, no doble clic) para editarlo
- O selecciónalo y presiona **F2**

---

# PARTE 5 — Crear los GameObjects principales

Vamos a crear los objetos necesarios uno por uno.

## 5.1 Crear el APIManager

Este objeto ejecuta el script que habla con la API Python.

1. Clic derecho en la Hierarchy → **Create Empty**
2. Renómbralo: `APIManager`
3. Con `APIManager` seleccionado, ve al **Inspector** (panel derecho)
4. Haz clic en **"Add Component"** (botón al fondo del Inspector)
5. Escribe `APIManager` en el buscador
6. Haz clic en **APIManager** (el script que creamos)

## 5.2 Crear el AppController

1. Clic derecho en Hierarchy → **Create Empty**
2. Renómbralo: `AppController`
3. En el Inspector → **Add Component** → busca `AppController` → selecciónalo

> **Nota:** Por ahora NO asignes las referencias de AppController. Lo haremos al final cuando todos los objetos existan.

## 5.3 Crear la cámara 3D

Esta cámara renderiza la visualización 3D de los datos de forma independiente.

1. En el menú: **GameObject → Camera**
2. Renómbrala: `Camera3D`
3. En el Inspector, busca la sección **"Camera"**:
   - **Clear Flags**: `Solid Color`
   - **Background**: haz clic en el cuadro de color → pon `R:17, G:17, B:17, A:255` (gris oscuro)
   - **Depth**: `1` (se renderiza encima de la cámara principal)

## 5.4 Crear Root3D

Es el contenedor vacío donde la visualización 3D creará los puntos y mallas.

1. Clic derecho en Hierarchy → **Create Empty**
2. Renómbralo: `Root3D`
3. En el Inspector, asegúrate que su **Transform** esté en posición `(0, 0, 0)`  
   (Si no, haz clic derecho sobre la palabra "Transform" → "Reset")

---

# PARTE 6 — Crear la capa "Visualization"

La cámara 3D solo debe ver los objetos de datos (puntos, mallas). Para lograrlo usamos "capas" (layers).

1. Ve a **Edit → Project Settings → Tags and Layers**
2. Busca la sección **"Layers"**
3. Verás que `User Layer 0` a `User Layer 7` ya existen (Default, TransparentFX, etc.)
4. En el primer slot vacío (probablemente `User Layer 8`) escribe: `Visualization`
5. Cierra Project Settings

**Ahora asigna la capa a Camera3D:**

1. Selecciona `Camera3D` en la Hierarchy
2. En el Inspector, busca **"Culling Mask"** (dentro del componente Camera)
3. Haz clic en el desplegable — verás todas las capas
4. Desmarca **todo** haciendo clic en "Nothing"
5. Luego marca solo **"Visualization"**

**Excluir la capa de la cámara principal:**

1. Selecciona `Main Camera` en la Hierarchy
2. En Inspector → Camera → **Culling Mask**
3. Haz clic → desmarca **"Visualization"**  
   (Deja marcadas todas las demás)

---

# PARTE 7 — Crear el RenderTexture para la vista 3D

Un RenderTexture es una "pantalla" en memoria donde Camera3D dibuja. Luego mostramos esa pantalla en la UI.

1. En la ventana **Project** (abajo), navega a la carpeta `Assets`
2. Clic derecho → **Create → Folder** → nómbrala `RenderTextures`
3. Entra a esa carpeta
4. Clic derecho → **Create → Render Texture**
5. Renómbrala: `RT_3D`
6. Selecciona `RT_3D` en el Project → en el Inspector:
   - **Size**: `1024 x 768`
   - **Depth Buffer**: `24 bit depth`

**Asignar el RenderTexture a Camera3D:**

1. Selecciona `Camera3D` en la Hierarchy
2. En Inspector → Camera → **Output Texture**
3. Haz clic en el círculo pequeño a la derecha del campo
4. Busca y selecciona `RT_3D`

---

# PARTE 8 — Crear el Canvas (interfaz de usuario)

## 8.1 Qué es un Canvas

El Canvas es el "lienzo" donde vive toda la interfaz de usuario (botones, paneles, textos). En Unity, la UI **siempre** vive dentro de un Canvas.

## 8.2 Crear el Canvas

1. Clic derecho en Hierarchy → **UI → Canvas**
2. Unity crea automáticamente `Canvas` y dentro un `EventSystem` (necesario para que los clicks funcionen — no lo toques)
3. Selecciona `Canvas` en la Hierarchy
4. En Inspector → Canvas:
   - **Render Mode**: `Screen Space - Overlay`
   - (Esto significa que la UI se dibuja encima de todo, en 2D)
5. Inspector → Canvas Scaler:
   - **UI Scale Mode**: `Scale With Screen Size`
   - **Reference Resolution**: `1920 x 1080`
   - **Match**: `0.5`

---

# PARTE 9 — Crear la StatusBar (barra de estado)

La barra de estado muestra mensajes como "CONECTANDO...", "ENTRENANDO...", etc.

## 9.1 Crear el panel de fondo

1. Clic derecho sobre `Canvas` en Hierarchy → **UI → Image**
2. Renómbrala: `StatusBar`
3. En Inspector → **Rect Transform** (la posición y tamaño en la UI):
   - Haz clic en el cuadrado de anclas (el cuadro con flechas en la esquina superior izquierda del Inspector)
   - Mantén **Alt** presionado y haz clic en la opción **"top-stretch"** (arriba al centro, estira horizontalmente)
   - **Height**: `60`
   - **Pos Y**: `-30` (o ajusta para que quede en la parte superior)
4. En Inspector → Image → **Color**: `R:26, G:26, B:46, A:255` (azul muy oscuro)

## 9.2 Agregar el indicador de estado (círculo de color)

1. Clic derecho sobre `StatusBar` → **UI → Image**
2. Renómbrala: `ImgIndicator`
3. Rect Transform:
   - **Width**: `16`, **Height**: `16`
   - Ancla: izquierda-centro (left-middle)
   - **Pos X**: `20`, **Pos Y**: `0`
4. Color: verde por defecto `R:46, G:204, B:113`

> Nota: El color del círculo cambia en tiempo de ejecución según el estado.

## 9.3 Agregar la barra de progreso

1. Clic derecho sobre `StatusBar` → **UI → Slider**
2. Renómbrala: `ProgressBar`
3. Rect Transform:
   - Ancla: **bottom-stretch** (estira abajo)
   - **Height**: `4`
   - **Pos Y**: `0`
4. En Inspector → Slider:
   - **Min Value**: `0`
   - **Max Value**: `1`
   - **Value**: `0`
   - **Interactable**: **desmarcado** (no es interactivo, solo visual)
5. Dentro del Slider hay hijos automáticos (`Background`, `Fill Area`, `Handle Slide Area`):
   - Selecciona `Handle Slide Area` → en Inspector → **desmarca** el checkbox del nombre (lo hace invisible, no queremos el handle)
   - Selecciona `Fill Area/Fill` → Image → Color: `R:52, G:152, B:219` (azul)

## 9.4 Agregar texto de estado principal

1. Clic derecho sobre `StatusBar` → **UI → Text - TextMeshPro**
   - Si aparece una ventana pidiendo importar TMP Essentials, haz clic en **"Import TMP Essentials"**
2. Renómbralo: `TxtStatus`
3. Rect Transform:
   - Ancla: **stretch-stretch** (ocupa todo)
   - **Left**: `45`, **Right**: `10`, **Top**: `5`, **Bottom**: `20`
4. En Inspector → TextMeshPro:
   - **Text**: `CONECTANDO...`
   - **Font Style**: **Bold**
   - **Font Size**: `13`
   - **Color**: `Blanco`
   - **Alignment**: izquierda-centro

## 9.5 Agregar texto de detalles

1. Clic derecho sobre `StatusBar` → **UI → Text - TextMeshPro**
2. Renómbralo: `TxtDetails`
3. Rect Transform:
   - Ancla: **bottom-stretch**
   - **Height**: `20`, **Left**: `45`, **Right**: `10`
   - **Pos Y**: `8`
4. TextMeshPro:
   - **Font Size**: `9`
   - **Color**: gris claro `R:180, G:180, B:180`
   - **Alignment**: izquierda-centro

## 9.6 Asignar el script StatusBar

1. Selecciona el GameObject `StatusBar`
2. Inspector → **Add Component** → busca `StatusBar` → selecciónalo
3. Ahora asigna las referencias arrastrando desde la Hierarchy:
   - **Txt Status**: arrastra `TxtStatus`
   - **Txt Details**: arrastra `TxtDetails`
   - **Img Indicator**: arrastra `ImgIndicator`
   - **Progress Bar**: arrastra `ProgressBar`

> **Cómo arrastrar**: haz clic sobre el objeto en la Hierarchy y sin soltar arrastra hasta el campo del Inspector. Suelta cuando el campo se ilumine.

---

# PARTE 10 — Crear el panel principal (layout izquierda/derecha)

## 10.1 Crear el contenedor principal

1. Clic derecho sobre `Canvas` → **UI → Image** (usaremos Image como fondo)
2. Renómbralo: `MainPanel`
3. Rect Transform:
   - Ancla: **stretch-stretch**
   - **Top**: `60` (para que empiece debajo de la StatusBar)
   - **Bottom**: `40` (espacio para botones de exportar abajo)
   - **Left**: `0`, **Right**: `0`
4. Image → Color: `R:13, G:13, B:26` (fondo muy oscuro)
5. Inspector → **Add Component** → busca **"Horizontal Layout Group"** → selecciónalo
6. En Horizontal Layout Group:
   - **Spacing**: `0`
   - **Child Alignment**: Upper Left
   - **Control Child Size**: marca **Width** y **Height**
   - **Child Force Expand**: marca **Width** y **Height**

---

# PARTE 11 — Panel izquierdo (configuración)

## 11.1 Crear el panel izquierdo con scroll

El panel izquierdo necesita scroll porque tiene muchas opciones.

1. Clic derecho sobre `MainPanel` → **UI → Scroll View**
2. Renómbralo: `LeftScroll`
3. Rect Transform: deja que el Layout Group lo controle
4. En Inspector → Scroll Rect:
   - **Horizontal**: desmarcado
   - **Vertical**: marcado
5. Selecciona `LeftScroll` → Inspector → **Layout Element** (Add Component):
   - **Preferred Width**: `400`
   - **Flexible Width**: `0`

Dentro de `LeftScroll` Unity crea automáticamente:
- `Viewport` → `Content`

6. Selecciona `Content`:
   - Add Component → **Vertical Layout Group**
     - **Spacing**: `5`
     - **Padding**: Top `10`, Bottom `10`, Left `10`, Right `10`
     - **Child Alignment**: Upper Center
     - **Control Child Size Width**: marcado
     - **Child Force Expand Width**: marcado, **Height**: desmarcado
   - Add Component → **Content Size Fitter**
     - **Vertical Fit**: `Preferred Size`

---

# PARTE 12 — Crear las secciones del panel izquierdo

Todas las secciones se crean como hijos de `Content`.

## 12.1 Cómo crear una "sección" (panel con fondo)

Cada sección de configuración es un panel con borde redondeado y fondo oscuro.

**Plantilla para crear una sección:**
1. Clic derecho sobre `Content` → **UI → Image**
2. Renómbrala (p.ej. `SeccionDatos`)
3. Image → Color: `R:20, G:20, B:40`
4. Add Component → **Vertical Layout Group**:
   - Spacing: `8`, Padding: todos a `12`
   - Control Child Size Width: marcado
   - Child Force Expand Width: marcado, Height: desmarcado
5. Add Component → **Layout Element**:
   - **Flexible Width**: `1`

**Agregar título a la sección:**
1. Clic derecho sobre la sección → **UI → Text - TextMeshPro**
2. Renómbralo `TxtTitulo`
3. TextMeshPro: texto en mayúsculas, Bold, tamaño `12`, color `R:100, G:180, B:255`

---

## 12.2 Sección DATOS — paso a paso completo

Crea la sección `SeccionDatos` siguiendo la plantilla anterior con título `"── DATOS ──"`.

### Dentro de `SeccionDatos` crea:

---

**A) Fila de número de clases**

1. Clic derecho sobre `SeccionDatos` → **UI → Image** → renombrar `RowClases`
2. Add Component → **Horizontal Layout Group** (Spacing: 8, Child Force Expand: ninguno)
3. Add Component → **Layout Element** (Preferred Height: 30)
4. Dentro de `RowClases`:
   - **UI → Text-TMP** → `LabelClases` → texto `"Nº Clases:"`
   - **UI → Slider** → `SliderClases`:
     - Min: `2`, Max: `10`, Whole Numbers: **marcado**, Value: `3`
     - Layout Element → Flexible Width: `1`
   - **UI → InputField - TextMeshPro** → `InputClases`:
     - Texto inicial: `"3"`, Content Type: `Integer Number`
     - Layout Element → Preferred Width: `50`

---

**B) Grupo: Instancias iguales o diferentes**

> Aquí necesitamos un ToggleGroup. Un ToggleGroup hace que solo un Toggle esté activo a la vez (como botones de radio).

1. Clic derecho sobre `SeccionDatos` → **Create Empty** → `GrupoInstancias`
2. En `GrupoInstancias`:
   - Add Component → **Toggle Group**
   - Add Component → **Horizontal Layout Group** (Spacing: 10)
   - Add Component → **Layout Element** (Preferred Height: 30)

3. Dentro de `GrupoInstancias`:
   - **UI → Toggle** → renombrar `ToggleInstIguales`
     - En Inspector → Toggle → **Group**: arrastra `GrupoInstancias`
     - **Is On**: **marcado** (activo por defecto)
     - Haz clic en el hijo `Label` → TextMeshPro → texto `"Igual por clase"`
   - **UI → Toggle** → renombrar `ToggleInstDiferentes`
     - Toggle → **Group**: arrastra `GrupoInstancias`
     - **Is On**: **desmarcado**
     - Label → texto `"Diferente por clase"`

4. **PanelInstIguales** (visible cuando "Igual" está activo):
   - Clic derecho sobre `SeccionDatos` → **UI → Image** → `PanelInstIguales`
   - Add Component → Horizontal Layout Group (Spacing: 8)
   - Add Component → Layout Element (Preferred Height: 30)
   - Dentro:
     - **UI → Text-TMP** → texto `"Total instancias:"`
     - **UI → InputField-TMP** → `InputInstIguales` → texto `"100"`, Content Type: Integer
       - Layout Element → Flexible Width: `1`

5. **PanelInstDiferentes** (visible cuando "Diferente" está activo — oculto por defecto):
   - Clic derecho sobre `SeccionDatos` → **UI → Scroll View** → `PanelInstDiferentes`
   - Layout Element → Preferred Height: `120`
   - Dentro de su `Viewport/Content`:
     - Add Component → Vertical Layout Group (Spacing: 4, Padding: 4)
     - Add Component → Content Size Fitter (Vertical Fit: Preferred Size)
     - Renombra este `Content` como `ContainerInstDifer`
   - **En el Inspector del GameObject `PanelInstDiferentes`**:  
     - Desmarca la casilla al lado del nombre (`☐ PanelInstDiferentes`) para **ocultarlo**  
     (Es el checkbox en la esquina superior izquierda del Inspector)

---

**C) Grupo: Separabilidad**

1. Crea `GrupoSepModo` en `SeccionDatos` (igual que GrupoInstancias):
   - Add Component → Toggle Group
   - Add Component → Horizontal Layout Group
   - Layout Element → Preferred Height: 30

2. Dentro de `GrupoSepModo`:
   - `ToggleSepAuto` → Label: `"Separabilidad automática"`, Is On: desmarcado
   - `ToggleSepManual` → Label: `"Separabilidad manual"`, Is On: **marcado**
   - Ambos → Toggle Group: arrastra `GrupoSepModo`

3. **PanelSepManual** (visible cuando "Manual"):
   - Clic derecho en `SeccionDatos` → **UI → Image** → `PanelSepManual`
   - Color: `R:15, G:15, B:30`
   - Add Component → Vertical Layout Group (Spacing: 6, Padding: 8)
   - Add Component → Layout Element

   Dentro de `PanelSepManual`:

   a) **GrupoSepTipo** (Global vs Por clase):
   - Create Empty → `GrupoSepTipo`
   - Add Component → Toggle Group
   - Add Component → Horizontal Layout Group, Layout Element (Height: 25)
   - Dentro:
     - `ToggleSepGlobal` → Label: `"Global"`, Is On: **marcado**, Group: GrupoSepTipo
     - `ToggleSepPorClase` → Label: `"Por clase"`, Is On: desmarcado, Group: GrupoSepTipo

   b) **PanelSepGlobal** (visible cuando "Global"):
   - UI → Image → `PanelSepGlobal`
   - Add Component → Vertical Layout Group (Spacing: 6)
   - Dentro crea dos filas idénticas (una para Mean Sep, otra para Std Dev):

   **Fila Mean Sep:**
   - Create Empty → `RowMeanSep` → Add HorizontalLayoutGroup + LayoutElement(Height:25)
   - Dentro: Label texto `"Separación media:"` + Slider `SliderMeanSep`(min=0.5,max=6,value=2.5) + InputField `InputMeanSep` texto "2.50" (Width:55)

   **Fila Std Dev:**
   - Create Empty → `RowStdDev` → Add HorizontalLayoutGroup + LayoutElement(Height:25)
   - Dentro: Label texto `"Desviación estándar:"` + Slider `SliderStdDev`(min=0.1,max=2.5,value=0.6) + InputField `InputStdDev` texto "0.60" (Width:55)

   c) **PanelSepPorClase** (oculto por defecto):
   - UI → Scroll View → `PanelSepPorClase`, Layout Element (Preferred Height: 150)
   - En su `Viewport/Content`: Add Vertical Layout Group + Content Size Fitter
   - Renombra ese Content como `ContainerSepClases`
   - Oculta el panel: desmarca la casilla del nombre en el Inspector

---

**D) Semilla aleatoria**

- Create Empty en `SeccionDatos` → `RowSeed` → HorizontalLayoutGroup + LayoutElement(Height:25)
- Dentro: Label texto `"Semilla:"` + InputField `InputSeed` texto "42" (Content Type: Integer)

---

**E) Botón Generar**

1. Clic derecho en `SeccionDatos` → **UI → Button - TextMeshPro** → `BtnGenerate`
2. Layout Element → Preferred Height: `40`
3. El botón tiene un hijo `Text (TMP)` — ponle texto `"GENERAR DATOS"`
4. Image del botón → Color: `R:39, G:174, B:96` (verde)

---

## 12.3 Sección RED NEURONAL

Crea `SeccionRed` como otra sección (mismo proceso que SeccionDatos) con título `"── RED NEURONAL ──"`.

### Dentro de `SeccionRed`:

**A) Capas de la red**

1. `GrupoCapas` → Toggle Group + HorizontalLayoutGroup + LayoutElement(Height:25)
   - `ToggleCapasAuto` → Label: `"Automático"`, Is On: **marcado**, Group: GrupoCapas
   - `ToggleCapasManual` → Label: `"Manual"`, Is On: desmarcado, Group: GrupoCapas

2. `PanelCapasManual` (oculto por defecto):
   - UI → Image → `PanelCapasManual`, Layout Element (Height: 35)
   - Dentro: Label texto `"Capas ocultas (ej: 16,8):"` + InputField `InputCapasManuales` placeholder "16,8"
   - Oculta el panel

**B) Activación**

1. `GrupoActiv` → Toggle Group + HorizontalLayoutGroup + LayoutElement(Height:25)
   - `ToggleActAuto` → Label: `"Activación auto (ReLU)"`, Is On: **marcado**, Group: GrupoActiv
   - `ToggleActManual` → Label: `"Manual"`, Is On: desmarcado, Group: GrupoActiv

2. `PanelActManual` (oculto por defecto):
   - UI → Image → `PanelActManual`, Layout Element (Height: 35)
   - Dentro: Label `"Función:"` + Dropdown-TMP `DropdownActivacion`
     - En el Dropdown, en el Inspector → **Options**: agrega `"ReLU"`, `"Sigmoid"`, `"Tanh"`
   - Oculta el panel

**C) Hiperparámetros** (4 filas, todas iguales):

Para cada uno crea una fila `RowXxx` con HorizontalLayoutGroup + LayoutElement(Height:25):
- `RowLR`: Label `"Learning Rate:"` + InputField `InputLearningRate` texto `"0.0100"`
- `RowEpochs`: Label `"Épocas:"` + InputField `InputEpochs` texto `"1000"`
- `RowMomentum`: Label `"Momentum:"` + InputField `InputMomentum` texto `"0.90"`
- `RowRecord`: Label `"Registrar cada:"` + InputField `InputRecordEvery` texto `"10"`

**D) Botón Entrenar**

- UI → Button-TMP → `BtnTrain`, Layout Element (Height: 40)
- Texto: `"ENTRENAR"`, Color: `R:41, G:128, B:185` (azul)

---

## 12.4 Asignar el script ConfigPanel

1. Selecciona el GameObject `Content` (dentro de LeftScroll/Viewport)
2. Inspector → **Add Component** → busca `ConfigPanel` → selecciónalo
3. Ahora asigna **cada campo** arrastrando desde la Hierarchy:

| Campo en Inspector | Arrastra este objeto |
|---|---|
| **Slider Clases** | `RowClases/SliderClases` |
| **Input Clases** | `RowClases/InputClases` |
| **Toggle Inst Iguales** | `GrupoInstancias/ToggleInstIguales` |
| **Toggle Inst Diferentes** | `GrupoInstancias/ToggleInstDiferentes` |
| **Panel Inst Iguales** | `PanelInstIguales` |
| **Input Inst Iguales** | `PanelInstIguales/InputInstIguales` |
| **Panel Inst Diferentes** | `PanelInstDiferentes` |
| **Container Inst Difer** | `PanelInstDiferentes/Viewport/ContainerInstDifer` |
| **Prefab Inst Row** | El prefab `InstRow` (lo creamos en Parte 15) |
| **Toggle Sep Auto** | `GrupoSepModo/ToggleSepAuto` |
| **Toggle Sep Manual** | `GrupoSepModo/ToggleSepManual` |
| **Panel Sep Manual** | `PanelSepManual` |
| **Toggle Sep Global** | `GrupoSepTipo/ToggleSepGlobal` |
| **Toggle Sep Por Clase** | `GrupoSepTipo/ToggleSepPorClase` |
| **Panel Sep Global** | `PanelSepGlobal` |
| **Slider Mean Sep** | `PanelSepGlobal/RowMeanSep/SliderMeanSep` |
| **Input Mean Sep** | `PanelSepGlobal/RowMeanSep/InputMeanSep` |
| **Slider Std Dev** | `PanelSepGlobal/RowStdDev/SliderStdDev` |
| **Input Std Dev** | `PanelSepGlobal/RowStdDev/InputStdDev` |
| **Panel Sep Por Clase** | `PanelSepPorClase` |
| **Container Sep Clases** | `PanelSepPorClase/Viewport/ContainerSepClases` |
| **Prefab Sep Row** | El prefab `SepRow` (lo creamos en Parte 15) |
| **Input Seed** | `RowSeed/InputSeed` |
| **Toggle Capas Auto** | `GrupoCapas/ToggleCapasAuto` |
| **Toggle Capas Manual** | `GrupoCapas/ToggleCapasManual` |
| **Panel Capas Manual** | `PanelCapasManual` |
| **Input Capas Manuales** | `PanelCapasManual/InputCapasManuales` |
| **Toggle Act Auto** | `GrupoActiv/ToggleActAuto` |
| **Toggle Act Manual** | `GrupoActiv/ToggleActManual` |
| **Panel Act Manual** | `PanelActManual` |
| **Dropdown Activacion** | `PanelActManual/DropdownActivacion` |
| **Input Learning Rate** | `RowLR/InputLearningRate` |
| **Input Epochs** | `RowEpochs/InputEpochs` |
| **Input Momentum** | `RowMomentum/InputMomentum` |
| **Input Record Every** | `RowRecord/InputRecordEvery` |

---

# PARTE 13 — Panel derecho (resultados y visualización)

## 13.1 Crear el panel derecho

1. Clic derecho sobre `MainPanel` → **UI → Image** → `RightPanel`
2. Add Component → **Vertical Layout Group** (Spacing: 0, Control Child Size: ambos, Force Expand: ambos)
3. Add Component → **Layout Element** (Flexible Width: 1)

## 13.2 Crear la barra de pestañas (tabs)

1. Clic derecho sobre `RightPanel` → **UI → Image** → `TabBar`
2. Color: `R:20, G:20, B:40`
3. Add Component → Horizontal Layout Group (Spacing: 2, Padding: 4)
4. Add Component → Layout Element (Preferred Height: 40, Flexible Width: 1)

5. Dentro de `TabBar` crea 4 botones:
   - **UI → Button-TMP** → `BtnTabGraficas` → Texto: `"📊 GRÁFICAS"`
   - **UI → Button-TMP** → `BtnTab3D` → Texto: `"🔷 VISTA 3D"`
   - **UI → Button-TMP** → `BtnTabClasificar` → Texto: `"🔍 CLASIFICAR"`
   - **UI → Button-TMP** → `BtnTabResultados` → Texto: `"📋 RESULTADOS"`

   Para cada botón: Add Component → Layout Element (Flexible Width: 1)

## 13.3 Área de contenido (donde se muestran los paneles)

1. Clic derecho sobre `RightPanel` → **UI → Image** → `ContentArea`
2. Color: `R:13, G:13, B:26`
3. Add Component → Layout Element (Flexible Height: 1, Flexible Width: 1)

Todos los paneles de abajo van dentro de `ContentArea`.

---

# PARTE 14 — Los 4 paneles del área de contenido

Todos los paneles van dentro de `ContentArea`. **Todos empiezan ocultos** (desmarca su casilla en el Inspector) excepto el primero que quieras mostrar por defecto.

## 14.1 GraphsPanel (gráficas PNG)

1. Clic derecho en `ContentArea` → **UI → Image** → `GraphsPanel`
2. Rect Transform: ancla **stretch-stretch**, todos los márgenes a 0
3. Add Component → Vertical Layout Group (Spacing: 8, Padding: 10)
4. Dentro:

   a) `TxtGraphTitle` — UI → Text-TMP, texto "Selecciona una gráfica", Bold, 14pt
   
   b) `ImgGraph` — UI → Raw Image
      - Add Component → **Aspect Ratio Fitter** (Aspect Mode: Fit In Parent)
      - Add Component → Layout Element (Flexible Height: 1, Flexible Width: 1)
   
   c) `BtnBarGraficas` — Create Empty, HorizontalLayoutGroup, Layout Element (Height: 40)
      Dentro:
      - Button-TMP → `BtnErrorPlot` → texto `"Pérdida / Precisión"`
      - Button-TMP → `BtnWeightsPlot` → texto `"Evolución Pesos"`
      - Button-TMP → `BtnConfusionPlot` → texto `"Matriz Confusión"`
      Para cada botón: Layout Element (Flexible Width: 1)

5. **Asignar script GraphDisplay:**
   - Selecciona `GraphsPanel`
   - Add Component → `GraphDisplay`
   - **Img Graph**: arrastra `ImgGraph`
   - **Panel Graphs**: arrastra `GraphsPanel` (el mismo objeto)
   - **Txt Graph Title**: arrastra `TxtGraphTitle`

## 14.2 View3DPanel (visualización 3D)

1. Clic derecho en `ContentArea` → **UI → Raw Image** → `View3DPanel`
2. Rect Transform: ancla stretch-stretch
3. En Inspector → Raw Image → **Texture**: haz clic en el círculo → selecciona `RT_3D`
4. **Oculta el panel** (desmarca la casilla)

Este panel simplemente muestra lo que Camera3D renderiza sobre RT_3D.

## 14.3 ClassifyPanel (clasificar puntos)

1. Clic derecho en `ContentArea` → **UI → Image** → `ClassifyPanel`
2. Rect Transform: ancla stretch-stretch
3. Add Component → Vertical Layout Group (Spacing: 10, Padding: 20)
4. **Oculta el panel**

Dentro crea:

a) Tres filas de entrada (una por coordenada):
   - Create Empty → `RowX1` → HorizontalLayoutGroup + LayoutElement(Height: 35)
     - Dentro: Label texto `"X₁:"` + InputField `InputX1` (Content Type: Decimal)
   - Igual para `RowX2` con `InputX2` y `RowX3` con `InputX3`

b) Botón:
   - Button-TMP → `BtnClassify` → texto `"CLASIFICAR"`, Color morado `R:142, G:68, B:173`
   - Layout Element (Preferred Height: 45)

c) Resultado:
   - Text-TMP → `TxtResult` → texto vacío, Bold, 18pt, alineación centro
   - Layout Element (Preferred Height: 50)

d) Probabilidades:
   - Text-TMP → `TxtProbabilities` → texto vacío, 10pt, alineación centro

e) Color de clase:
   - UI → Image → `ImgClassColor` → Layout Element (Width: 30, Height: 30)

**Asignar script ClassifyPanel:**
- Selecciona `ClassifyPanel`
- Add Component → `ClassifyPanel`
- Arrastra los campos correspondientes

## 14.4 ResultsPanel (métricas del modelo)

1. Clic derecho en `ContentArea` → **UI → Image** → `ResultsPanel`
2. Rect Transform: ancla stretch-stretch
3. Add Component → Vertical Layout Group (Spacing: 8, Padding: 15)
4. **Oculta el panel**

Dentro crea:

a) Crear un contenedor `PanelResults` (UI → Image, stretch-stretch, VerticalLayoutGroup)
   (Este es el que se muestra/oculta cuando hay resultados)

Dentro de `PanelResults`:
   - Text-TMP → `TxtAccuracy` → texto `"Precisión: -"`, Bold, 16pt
   - Text-TMP → `TxtArchitecture` → texto `"Arquitectura: -"`, 11pt
   - Text-TMP → `TxtActivation` → texto `"Activación: -"`, 11pt
   - Text-TMP → `TxtEpochs` → texto `"Épocas: -"`, 11pt
   - Text-TMP → `TxtParams` → texto `"Parámetros: -"`, 11pt
   - Create Empty → `TableContainer` → VerticalLayoutGroup + ContentSizeFitter(Vertical: Preferred)

**Asignar script ResultsPanel:**
- Selecciona `ResultsPanel`
- Add Component → `ResultsPanel`
- Asigna los campos:
  - **Panel Results**: arrastra `PanelResults`
  - **Txt Accuracy**: arrastra `TxtAccuracy`
  - **Txt Architecture**: arrastra `TxtArchitecture`
  - **Txt Activation**: arrastra `TxtActivation`
  - **Txt Epochs**: arrastra `TxtEpochs`
  - **Txt Params**: arrastra `TxtParams`
  - **Table Container**: arrastra `TableContainer`
  - **Row Prefab**: el prefab `ResultRow` (lo creamos en Parte 15)

---

# PARTE 15 — Crear la barra de exportación

1. Clic derecho sobre `Canvas` → **UI → Image** → `ExportBar`
2. Rect Transform: ancla **bottom-stretch**, Height: `40`, Pos Y: `0`
3. Add Component → Horizontal Layout Group (Spacing: 10, Padding: 5, Child Force Expand Width: true)
4. Dentro:
   - Button-TMP → `BtnCSV` → texto `"💾 Exportar CSV"`, Color: `R:39, G:174, B:96`
   - Button-TMP → `BtnPDF` → texto `"📄 Exportar PDF"`, Color: `R:192, G:57, B:43`

---

# PARTE 16 — Crear los 3 Prefabs necesarios

> Un **Prefab** es una plantilla reutilizable de un GameObject. Cuando el script necesita crear muchas filas dinámicamente (ej: una fila por clase), usa un prefab como molde.

## 16.1 Preparar la carpeta de Prefabs

1. En la ventana **Project** (abajo), haz clic derecho en `Assets`
2. **Create → Folder** → nómbrala `Prefabs`

## 16.2 Prefab "InstRow" (fila de instancias por clase)

Este prefab se usará para la sección "Instancias diferentes por clase".

**Crea el objeto:**
1. Clic derecho en Hierarchy (NO dentro de ningún panel) → Create Empty → `InstRow`
2. Add Component → Horizontal Layout Group (Spacing: 8, Padding: 4)
3. Add Component → Layout Element (Preferred Height: 30, Flexible Width: 1)
4. Dentro de `InstRow`:
   - **UI → Text-TMP** → `LabelClase`
     - Texto: `"Clase:"` (el script lo cambiará a "Clase 0:", "Clase 1:", etc.)
     - Layout Element → Preferred Width: `60`
   - **UI → InputField-TMP** → `InputInst`
     - Texto: `"100"`
     - Content Type: `Integer Number`
     - Add Component → Layout Element → Flexible Width: `1`

**Convertir en Prefab:**
1. Arrastra `InstRow` desde la **Hierarchy** hasta la carpeta `Assets/Prefabs` en la ventana **Project**
2. El nombre en la Hierarchy se pondrá azul (indica que es una instancia de prefab)
3. Elimina el objeto de la Hierarchy: clic derecho → Delete  
   (El prefab queda guardado en la carpeta)

## 16.3 Prefab "SepRow" (fila de separabilidad por clase)

1. Create Empty en Hierarchy → `SepRow`
2. Add Component → Vertical Layout Group (Spacing: 4, Padding: 4)
3. Add Component → Layout Element (Preferred Height: 70, Flexible Width: 1)
4. Dentro:
   - **UI → Text-TMP** → `LabelClaseSep` → texto `"Clase:"`
   - **UI → Slider** → `SliderMeanSep`
     - Min: `0.5`, Max: `6`, Value: `2.5`, Whole Numbers: desmarcado
     - Add Component → Layout Element (Preferred Height: 20)
   - **UI → Slider** → `SliderStdDev`
     - Min: `0.1`, Max: `2.5`, Value: `0.6`
     - Add Component → Layout Element (Preferred Height: 20)

**Convertir en Prefab:** arrastra desde Hierarchy a `Assets/Prefabs` → borra de Hierarchy

## 16.4 Prefab "ResultRow" (fila de métricas por clase)

1. Create Empty en Hierarchy → `ResultRow`
2. Add Component → Horizontal Layout Group (Spacing: 5)
3. Add Component → Layout Element (Preferred Height: 25, Flexible Width: 1)
4. Dentro, crea 6 hijos (todos UI → Text-TMP):
   - `ColClase` → texto `"Clase"`, Layout Element (Preferred Width: 60)
   - `ColTotal` → texto `"Total"`, Layout Element (Flexible Width: 1)
   - `ColCorrectas` → texto `"Correctas"`, Layout Element (Flexible Width: 1)
   - `ColPrecision` → texto `"Precisión"`, Layout Element (Flexible Width: 1)
   - `ColRecall` → texto `"Recall"`, Layout Element (Flexible Width: 1)
   - `ColF1` → texto `"F1"`, Layout Element (Flexible Width: 1)

**Convertir en Prefab:** arrastra a `Assets/Prefabs` → borra de Hierarchy

---

# PARTE 17 — Asignar scripts a Camera3D y Root3D

## 17.1 Script Visualizer3D

1. Selecciona `Root3D` en la Hierarchy
2. Add Component → `Visualizer3D`
3. Asigna:
   - **Root 3D**: arrastra `Root3D` (el mismo)
   - **Render Texture**: arrastra `RT_3D` desde `Assets/RenderTextures`
   - **Cam 3D**: arrastra `Camera3D`

## 17.2 Script CameraOrbit

1. Selecciona `Camera3D` en la Hierarchy
2. Add Component → `CameraOrbit`
3. Asigna:
   - **Target**: arrastra `Root3D`
   - **Orbit Speed**: `200`
   - **Zoom Speed**: `5`
   - **Pan Speed**: `0.02`
   - **Min Distance**: `3`
   - **Max Distance**: `25`
   - **Viewport Rect**: arrastra `View3DPanel` (el RawImage que muestra la vista 3D)

---

# PARTE 18 — Conectar el AppController

Ahora que todos los objetos existen, podemos asignar las referencias.

1. Selecciona `AppController` en la Hierarchy
2. En el Inspector, verás el componente `AppController` con campos vacíos:

| Campo | Arrastra este objeto |
|---|---|
| **Status Bar** | `StatusBar` |
| **Config Panel** | `Content` (el que tiene el script ConfigPanel) |
| **Graph Display** | `GraphsPanel` |
| **Visualizer 3D** | `Root3D` (el que tiene Visualizer3D) |
| **Classify Panel** | `ClassifyPanel` |
| **Results Panel** | `ResultsPanel` |

---

# PARTE 19 — Conectar todos los botones

Los botones necesitan saber qué función llamar cuando el usuario hace clic.

**Para cada botón:**
1. Selecciona el botón en la Hierarchy
2. En Inspector → **Button** → sección **On Click ()**
3. Haz clic en **"+"** para agregar una entrada
4. En el campo donde pone **None (Object)**: arrastra `AppController`
5. En el desplegable **"No Function"**: selecciona la función correspondiente

| Botón | Función en AppController |
|---|---|
| `BtnGenerate` | `OnGenerateClicked` |
| `BtnTrain` | `OnTrainClicked` |
| `BtnErrorPlot` | `OnShowErrorPlot` |
| `BtnWeightsPlot` | `OnShowWeightsPlot` |
| `BtnConfusionPlot` | `OnShowConfusionPlot` |
| `BtnCSV` | `OnExportCSV` |
| `BtnPDF` | `OnExportPDF` |

**Los botones de tab** no llaman a AppController. Llaman a `SetActive()` para mostrar/ocultar paneles:

Para `BtnTabGraficas`:
1. On Click → "+" → arrastra `GraphsPanel` → selecciona **GameObject → SetActive (bool)** → marca el checkbox
2. On Click → "+" → arrastra `View3DPanel` → **GameObject → SetActive** → **desmarca** el checkbox
3. Repite para `ClassifyPanel` y `ResultsPanel` (desmarcados)

Para `BtnTab3D`:
1. On Click → `AppController.OnShow3D` (descarga datos y renderiza)
2. On Click → arrastra `View3DPanel` → SetActive → **marcado**
3. Oculta los demás

Para `BtnTabClasificar`:
1. On Click → `ClassifyPanel` → SetActive → marcado
2. Oculta los demás

Para `BtnTabResultados`:
1. On Click → `ResultsPanel` → SetActive → marcado
2. Oculta los demás

Para `BtnClassify`:
1. On Click → arrastra `AppController` → `OnClassifyClicked`

---

# PARTE 20 — Asignar los Prefabs en ConfigPanel y ResultsPanel

Ahora que los prefabs están creados:

**En ConfigPanel** (en el objeto `Content`):
1. Selecciona `Content`
2. En el componente `ConfigPanel`:
   - **Prefab Inst Row**: arrastra `Assets/Prefabs/InstRow.prefab` desde la ventana Project
   - **Prefab Sep Row**: arrastra `Assets/Prefabs/SepRow.prefab`

**En ResultsPanel**:
1. Selecciona `ResultsPanel`
2. En el componente `ResultsPanel`:
   - **Row Prefab**: arrastra `Assets/Prefabs/ResultRow.prefab`

---

# PARTE 21 — Verificación final antes de ejecutar

## 21.1 Lista de comprobación

Antes de presionar Play, verifica cada punto:

- [ ] El backend Python está corriendo en `localhost:8000`
- [ ] En Project Settings → Player: Input Handling = "Input System Package (New)"
- [ ] `Camera3D` tiene Output Texture = `RT_3D` y Culling Mask = solo "Visualization"
- [ ] `Main Camera` tiene Culling Mask sin "Visualization"
- [ ] `View3DPanel` (RawImage) tiene Texture = `RT_3D`
- [ ] `APIManager` tiene el script `APIManager` asignado
- [ ] `AppController` tiene todos los campos asignados
- [ ] `ConfigPanel` tiene **todos** los campos asignados (revisar que ninguno quede vacío)
- [ ] Los 3 prefabs están en `Assets/Prefabs/` y asignados a sus scripts
- [ ] Todos los botones tienen sus funciones OnClick conectadas
- [ ] `GraphsPanel`, `View3DPanel`, `ClassifyPanel`, `ResultsPanel` están **ocultos** al inicio

## 21.2 Ejecutar el proyecto

1. Presiona el botón **▶ Play** (triángulo en la barra superior)
2. Unity entra en modo de ejecución (el editor se pone con borde azul/gris)
3. Deberías ver la StatusBar con **"CONECTANDO..."**
4. En 1-2 segundos cambia a **"CONECTADO — API disponible"**

Si no conecta, verifica que el backend esté corriendo.

## 21.3 Flujo de prueba completo

```
1. Panel izquierdo → ajusta los parámetros:
   - Clases: 3
   - Instancias: 150 (igual por clase)
   - Separabilidad: Manual, Global, Mean Sep = 2.5, Std Dev = 0.6
   - Semilla: 42

2. Haz clic en "GENERAR DATOS"
   → StatusBar: "GENERANDO DATOS..."
   → Luego: "DATOS GENERADOS: 450 instancias | Separabilidad: Alta"

3. Configuración de red:
   - Capas: Automático
   - Activación: Auto (ReLU)
   - Learning Rate: 0.01, Épocas: 1000, Momentum: 0.9

4. Haz clic en "ENTRENAR"
   → StatusBar: "ENTRENANDO... ÉPOCA 100/1000 | PÉRDIDA: 0.5678 | PRECISIÓN: 78.3%"
   → La barra de progreso avanza
   → Al terminar: "✅ ENTRENAMIENTO FINALIZADO | PRECISIÓN: 97.50%"
   → El panel de Resultados se muestra automáticamente

5. Tab "GRÁFICAS":
   → Clic en "Pérdida / Precisión" → ve la curva de aprendizaje
   → Clic en "Evolución Pesos" → ve cómo cambiaron los pesos
   → Clic en "Matriz Confusión" → ve el rendimiento por clase

6. Tab "VISTA 3D":
   → Clic izquierdo + arrastrar = orbitar el cubo
   → Rueda del ratón = zoom
   → Clic derecho + arrastrar = mover el punto de vista
   → Verás los puntos de datos y las superficies de decisión curvas

7. Tab "CLASIFICAR":
   → Ingresa X1: 1.5, X2: 0.5, X3: -1.0
   → Clic en "CLASIFICAR"
   → Verás: "CLASE 2 — 94.37% confianza"
   → Y las probabilidades de cada clase

8. Exportar:
   → "Exportar CSV" → guarda el archivo en el PC
   → "Exportar PDF" → guarda el reporte completo
```

---

# PARTE 22 — Solución de problemas

| Problema | Causa probable | Solución |
|---|---|---|
| StatusBar siempre en "CONECTANDO..." | Backend no está corriendo | Ejecuta el backend en terminal |
| Error "Enter Safe Mode" al abrir | Errores de compilación | Revisa la consola de Unity (Console tab abajo) |
| La vista 3D está en negro | Camera3D no ve la capa correcta | Verifica Culling Mask en Camera3D |
| Los objetos 3D no aparecen | RenderTexture no asignada | Asegúrate que View3DPanel.Texture = RT_3D |
| Los botones no hacen nada | OnClick no conectado | Selecciona el botón, revisa Inspector → On Click |
| "Input Manager" warning | Input System mal configurado | Project Settings → Player → New Input System |
| Los paneles no se muestran/ocultan | SetActive mal configurado en tabs | Revisa los eventos OnClick de cada botón Tab |
| NullReferenceException en consola | Un campo del Inspector está vacío | Busca el componente indicado, asigna el campo faltante |
| El entrenamiento no termina | Muchas épocas o dataset grande | Normal — espera o reduce las épocas |
| Timeout en exportar PDF | Timeout de 30 segundos | El PDF tarda más — es una limitación actual |

---

# Estructura final de la Hierarchy

```
Scene
├── Main Camera             (cámara UI principal)
├── Directional Light       (luz de la escena)
├── Camera3D                (renderiza la viz 3D → RT_3D)
│   └── [CameraOrbit.cs]
├── Root3D                  (padre de puntos/mallas 3D)
│   └── [Visualizer3D.cs]
├── APIManager              (singleton HTTP)
│   └── [APIManager.cs]
├── AppController           (lógica principal)
│   └── [AppController.cs]
└── Canvas
    ├── EventSystem
    ├── StatusBar
    │   ├── ImgIndicator
    │   ├── ProgressBar
    │   ├── TxtStatus
    │   └── TxtDetails
    ├── MainPanel
    │   ├── LeftScroll
    │   │   └── Viewport
    │   │       └── Content  [ConfigPanel.cs]
    │   │           ├── SeccionDatos
    │   │           │   ├── RowClases
    │   │           │   ├── GrupoInstancias
    │   │           │   ├── PanelInstIguales
    │   │           │   ├── PanelInstDiferentes
    │   │           │   ├── GrupoSepModo
    │   │           │   ├── PanelSepManual
    │   │           │   │   ├── GrupoSepTipo
    │   │           │   │   ├── PanelSepGlobal
    │   │           │   │   └── PanelSepPorClase
    │   │           │   ├── RowSeed
    │   │           │   └── BtnGenerate
    │   │           └── SeccionRed
    │   │               ├── GrupoCapas
    │   │               ├── PanelCapasManual
    │   │               ├── GrupoActiv
    │   │               ├── PanelActManual
    │   │               ├── RowLR / RowEpochs / RowMomentum / RowRecord
    │   │               └── BtnTrain
    │   └── RightPanel
    │       ├── TabBar
    │       │   ├── BtnTabGraficas
    │       │   ├── BtnTab3D
    │       │   ├── BtnTabClasificar
    │       │   └── BtnTabResultados
    │       └── ContentArea
    │           ├── GraphsPanel     [GraphDisplay.cs]
    │           ├── View3DPanel     (RawImage con RT_3D)
    │           ├── ClassifyPanel   [ClassifyPanel.cs]
    │           └── ResultsPanel    [ResultsPanel.cs]
    └── ExportBar
        ├── BtnCSV
        └── BtnPDF
```
