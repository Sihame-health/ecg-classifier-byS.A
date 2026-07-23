# ECG Classifier — Normal/Abnormal Detection

A full-stack machine learning project that classifies ECG (electrocardiogram) images as **Normal** or **Abnormal** using a CNN model, served through a Python API and an ASP.NET Core web application.

🔗 **Live demo:** https://ecg-classifierabnormal-normal-bysa.up.railway.app
![Demo-ECG-CLASSIFIER](demo.gif)

---

## Overview

This project was built as a personal deep dive into applying machine learning to healthcare, combining a background in electrical engineering with a Master's in Digital Engineering for Healthcare Professions. It covers the full pipeline: dataset construction, model training, API development, and a deployed web interface.

## Features

- Upload an ECG image and get an instant **Normal / Abnormal** classification
- Confidence score displayed for each prediction
- Basic detection of non-ECG images ("Not an ECG" result)
- Robust error handling for corrupted or invalid files
- Responsive, mobile-friendly interface

## Tech Stack

**Machine Learning**
- Python, TensorFlow / Keras
- MobileNetV2 (transfer learning)
- FastAPI (prediction API)

**Web Application**
- C#, ASP.NET Core (Razor Pages)
- HTML / CSS / JavaScript

**Deployment**
- Railway (both API and web app)
- GitHub for version control

## Architecture
The web app and the ML API are deployed as two independent services, communicating over HTTP. This separation allows the model to be updated or improved independently from the web interface.

## Dataset

The training dataset (110,000+ images) was built by combining two sources:

1. **MIT-BIH Arrhythmia Database** — signal data converted into single-beat ECG images (with a simulated graph-paper background for visual consistency)
2. **Real-world ECG photographs** — a public dataset of scanned 12-lead ECG reports (Normal, Abnormal Heartbeat, Myocardial Infarction, History of MI)

Images were resized to 224×224, labels harmonized into a binary classification (Normal / Abnormal), and split into train/validation/test sets (70/15/15).

## Model Performance

| Metric | Score |
|---|---|
| AUC | 96.9% |
| Accuracy | 95.3% |
| Recall (Normal) | 97.7% |
| Recall (Abnormal) | 84.2% |

Several architectures were compared (MobileNetV2, EfficientNetB0, ResNet50, DenseNet121) via transfer learning; MobileNetV2 was selected for its best balance of accuracy and inference speed.

## Known Limitations

- The MIT-BIH portion of the dataset uses single-beat signals, not full multi-lead ECG traces — users should upload one lead at a time, not a complete 12-lead recording.
- The "Not an ECG" detection is a rule-based heuristic (based on color saturation and contrast), not a trained classifier. A dedicated model for this is a planned improvement.
- This tool is for educational purposes only and is **not** a diagnostic device. It does not replace professional medical advice.

## Running Locally

**API**
```bash
cd api
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt
python main.py
```

**Web App**
```bash
cd webApp/EcgClassifierWeb/EcgClassifierWeb
dotnet run
```

Update `appsettings.json` with your local API URL before running.

## Future Improvements

- Train a dedicated ECG vs. non-ECG classifier to replace the current heuristic
- Add automated tests (unit + integration)
- Support multi-lead ECG image analysis

## Author

**Siham Ait Taleb** — Master's student in Digital Engineering for Healthcare Professions, Morocco.
