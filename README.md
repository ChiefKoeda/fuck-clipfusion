# FUCK CLIPFUSION 🎬

Générateur automatique de vidéos avec captions — alternative gratuite et open-source à CapCut pour la création de contenu en masse.

## Fonctionnalités

- **Templates illimités** — crée des textes avec police, taille, couleur et style personnalisés
- **Prévisualisation live** — vois ton texte en temps réel sur ta vraie vidéo, glisse pour positionner, redimensionne avec la souris
- **Batch processing** — génère 10, 50, 100 vidéos automatiquement avec des combinaisons aléatoires
- **Musique de fond** — mixe automatiquement une piste audio sur toutes les vidéos
- **Emojis couleur** — support complet des emojis via SkiaSharp + Bahnschrift
- **Jobs parallèles** — traitement simultané de plusieurs vidéos pour aller vite
- **Accélération GPU** — support NVENC (NVIDIA) pour un rendu 5-10× plus rapide
- **Sauvegarde automatique** — tous tes templates et paramètres sont sauvegardés entre sessions

## Prérequis

- Windows 10/11 (64-bit)
- [FFmpeg](https://www.gyan.dev/ffmpeg/builds/) — à installer et ajouter au PATH, ou à configurer dans Options

## Installation

1. Télécharge le dernier `.exe` depuis [Releases](../../releases)
2. Lance `VideoMixer.exe` (aucune installation requise)
3. Configure le chemin FFmpeg dans **Options** si besoin

## Utilisation

1. **Templates** — crée tes textes, positionne-les sur la prévisualisation
2. **Vidéos** — sélectionne ton dossier de vidéos sources
3. **Musique** *(optionnel)* — ajoute une musique de fond
4. **Mixer** — configure le nombre de vidéos à générer et lance

## Tech stack

- .NET 8 + WPF (Windows)
- [SkiaSharp](https://github.com/mono/SkiaSharp) — rendu texte avec emojis couleur
- FFmpeg — encodage vidéo

## Build

```bash
dotnet publish VideoMixerWPF/VideoMixer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Licence

MIT — libre d'utilisation, modification et distribution.
