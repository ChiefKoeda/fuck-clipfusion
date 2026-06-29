# 🎬 VideoMixer — Logiciel PC Windows (C# WPF)

Logiciel Windows natif pour superposer des captions texte sur tes vidéos en batch.
Génère 10, 20, 50+ vidéos d'un seul clic avec assignation aléatoire template × vidéo.

---

## ✅ Prérequis

### 1. .NET 8 SDK
→ https://dotnet.microsoft.com/download/dotnet/8.0
Télécharge **".NET 8.0 SDK"** (Windows x64)

### 2. FFmpeg
→ https://www.gyan.dev/ffmpeg/builds/
Télécharge **"ffmpeg-release-essentials.zip"**, extrais et ajoute le dossier `bin/` à ton PATH Windows.

**Ou** : place `ffmpeg.exe` n'importe où et renseigne son chemin dans Options > FFmpeg.

---

## 🚀 Lancer le logiciel

```bash
# Dans le dossier VideoMixerWPF/
dotnet run
```

---

## 📦 Compiler en .exe autonome

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./dist
```

Le fichier `VideoMixer.exe` sera dans le dossier `dist/`.
Il tourne sans installer .NET sur la machine cible.

---

## 🎯 Comment utiliser

### Étape 1 — Templates
- Tape le texte de ta caption (supporte les retours à la ligne)
- Choisis la position (TopLeft, TopCenter, ... BotCenter...)
- Règle la taille, la couleur (#FFFFFF) et le style
- Clique **+ Ajouter le template**
- Répète pour tous tes templates

### Étape 2 — Vidéos
- Clique **📂 Choisir** → sélectionne le dossier de tes vidéos brutes
- Clique **🔍 Scanner** pour les détecter (MP4, MOV, AVI, MKV)

### Étape 3 — Musique (optionnel)
- Clique pour ajouter un MP3 en fond
- Règle le volume musique et le volume de la vidéo originale

### Étape 4 — Mixer
- Définis le nombre de vidéos à générer
- Choisis le dossier de sortie
- Clique **🚀 Lancer le mix**

VideoMixer assigne aléatoirement un template à chaque vidéo et génère tout en batch avec FFmpeg.

---

## 📁 Structure du projet

```
VideoMixerWPF/
├── App.xaml / App.xaml.cs          → Application WPF
├── Models/
│   ├── VideoTemplate.cs            → Modèle d'un template
│   └── MixJob.cs                   → Modèle d'un job de mix
├── ViewModels/
│   └── MainViewModel.cs            → Toute la logique métier (MVVM)
├── Views/
│   ├── MainWindow.xaml             → Interface utilisateur WPF
│   └── MainWindow.xaml.cs          → Code-behind (navigation, fenêtre)
├── Services/
│   └── MixerService.cs             → Moteur FFmpeg (génération vidéo)
├── Converters/
│   └── Converters.cs               → Convertisseurs de binding WPF
└── VideoMixer.csproj               → Fichier projet .NET 8
```

---

## 🎨 Design

- Interface sombre style ClipFusion
- Sidebar de navigation en 4 étapes
- Barre de stats en bas en temps réel
- Progression job par job avec console FFmpeg
- Thème MaterialDesignInXAML
