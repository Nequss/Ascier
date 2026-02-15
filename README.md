# Ascier Web

webowa aplikacja do konwersji obrazów i wideo na ascii art.
zoptymalizowana na operacjach bitowych i minimalnym zużyciu zasobów.

## demo

```
 ▄▀█ █▀ █▀▀ █ █▀▀ █▀█
 █▀█ ▄█ █▄▄ █ ██▄ █▀▄
```

## architektura

```
┌─────────────────────────────────────────────────────────┐
│                         vps                              │
│                                                          │
│  ┌────────────────┐  ┌──────────────┐  ┌─────────────┐ │
│  │  ascier_web    │  │ soldat_server│  │  m79_webapp  │ │
│  │  (port 5219)   │  │ (port 23073) │  │ (port 5218) │ │
│  │                │  └──────────────┘  └─────────────┘ │
│  │ • upload img/vid                                     │
│  │ • 10 efektów   │  ┌──────────────┐                  │
│  │ • realtime prev│  │ nequs-hq-bot │◄── monitoruje    │
│  │ • download .txt│  │  (discord)   │    logi ascier   │
│  └────────────────┘  └──────────────┘                  │
│           │                                              │
│           └── docker network: soldatserver_default       │
└─────────────────────────────────────────────────────────┘
```

## efekty ascii

| efekt | opis |
|-------|------|
| `classic` | luminancja mapowana na gęstość znaków ascii |
| `color` | jak classic ale z oryginalnymi kolorami pikseli |
| `edge` | operator sobela - detekcja krawędzi na operacjach całkowitoliczbowych |
| `matrix` | zielone kaskadowe znaki jak w filmie matrix |
| `dither` | floyd-steinberg z arytmetyką shift >> 4 zamiast /16 |
| `braille` | unicode braille ⠿ - 2x4 pikseli zakodowane jako bity w jednym znaku |
| `block` | elementy unicode ░▒▓█ - 5 poziomów cieniowania |
| `invert` | odwrócona luminancja (xor 0xff) |
| `threshold` | binarny próg - # lub spacja, branchless |
| `halftone` | ordered dithering z matrycą bayera 4x4 |

## optymalizacje bitowe

najważniejsze optymalizacje zastosowane w kodzie:

```
luminancja:     (r * 77 + g * 150 + b * 29) >> 8     // zamiast float * 0.299
mapowanie:      (val * charCount) >> 8                 // zamiast val / 25
clamp 0-255:    v &= ~(v >> 31); v-=255; v&=v>>31; v+=255  // bez rozgałęzień
wartość bezwzgl: (v ^ mask) - mask                     // bez if
próg binarny:   (threshold - val) >>> 31               // 1 bit, branchless
braille:        bits |= threshold_bit << bit_index     // 8 pikseli = 1 bajt
dithering:      (error * 7) >> 4                       // zamiast / 16
bayer index:    (y & 3) * 4 + (x & 3)                 // mod 4 bez dzielenia
inwersja:       lum ^ 0xFF                             // not na bitach
hash pozycji:   x * 73856093 ^ y * 19349669            // fibonacci multiply
```

## zużycie zasobów

- **cpu**: max 0.50 rdzenia (docker limit)
- **ram**: max 256mb (docker limit)
- **dysk**: cache klatek wideo w /tmp, auto-czyszczenie po 30 min
- **bufor pikseli**: ArrayPool<byte> - zero alokacji gc w hot path
- **brak float**: wszystkie obliczenia na int z operacjami bitowymi

## uruchomienie

### docker (zalecane)

```bash
cd Ascier/AscierWeb
docker compose up -d --build
```

dostępne pod: `http://twoj-ip:5219`

### wymagania

- docker 20.10+
- sieć docker `soldat_project_soldatserver_default` (tworzona automatycznie przez soldat_project)

### porty

| port | protokół | opis |
|------|----------|------|
| 5219 | tcp | ascier web app |

## api

| endpoint | metoda | opis |
|----------|--------|------|
| `/api/effects` | GET | lista dostępnych efektów |
| `/api/convert/image` | POST | konwersja obrazu (multipart/form-data) |
| `/api/convert/video` | POST | upload wideo, zwraca sessionId |
| `/api/convert/frame` | POST | konwersja klatki wideo |
| `/api/download/text` | POST | pobranie ascii jako .txt |
| `/hub/conversion` | SignalR | streaming klatek w czasie rzeczywistym |

### parametry konwersji (form-data)

| parametr | typ | domyślna | opis |
|----------|-----|----------|------|
| `file` | plik | - | obraz lub wideo |
| `effect` | string | classic | nazwa efektu |
| `step` | int | 8 | krok próbkowania (1-64 px) |
| `colorMode` | bool | false | zachowaj kolory |
| `threshold` | int | 128 | próg binarny (0-255) |
| `invert` | bool | false | odwróć luminancję |
| `maxColumns` | int | 300 | max szerokość wyjścia |

## integracja z infrastrukturą

### discord bot (nequs hq)
- monitoruje logi kontenera `ascier_web`
- śledzi commity w repo `Nequss/Ascier`
- wyświetla status w kanale `docker-status`

### docker network
- podłączony do `soldatserver_default` razem z resztą usług
- widoczny dla nginx proxy manager

## struktura projektu

```
AscierWeb/
├── Program.cs              # minimal api + endpointy
├── Dockerfile              # alpine + ffmpeg
├── docker-compose.yml      # konfiguracja kontenera
├── Core/
│   ├── BitOps.cs           # operacje bitowe
│   ├── AsciiMapper.cs      # mapowanie luminancji na znaki
│   ├── AsciiFrame.cs       # struktura wyjściowa
│   ├── ConversionSettings.cs
│   ├── EffectRegistry.cs   # rejestr efektów
│   └── Effects/
│       ├── IEffect.cs
│       ├── ClassicEffect.cs
│       ├── ColorEffect.cs
│       ├── EdgeEffect.cs
│       ├── MatrixEffect.cs
│       ├── DitherEffect.cs
│       ├── BrailleEffect.cs
│       ├── BlockEffect.cs
│       ├── InvertEffect.cs
│       ├── ThresholdEffect.cs
│       └── HalftoneEffect.cs
├── Services/
│   ├── ImageService.cs     # dekodowanie obrazów
│   ├── VideoService.cs     # ekstrakcja klatek ffmpeg
│   └── ConversionHub.cs    # signalr hub
└── wwwroot/
    ├── index.html
    ├── css/style.css
    └── js/app.js
```

## różnice vs oryginalny ascier

| cecha | oryginał | web |
|-------|----------|-----|
| platforma | desktop (sfml) | web (asp.net core) |
| rendering | sfml window | canvas + signalr |
| zależności | sfml, magick.net, xabe.ffmpeg, cli_sharp | imagesharp, ffmpeg cli |
| greyscale | `(r+g+b)/3` | `(r*77+g*150+b*29)>>8` |
| char mapping | `grey/25` | `(grey*n)>>8` |
| efekty | 2 (color/grey) | 10 |
| pamięć | ładuje wszystko | arraypool + streaming |
| wideo | extractEveryNthFrame | seek na żądanie |
| target | .net 5.0 | .net 8.0 |
| komentarze | angielski | polski lowercase |





