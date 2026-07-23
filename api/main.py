from fastapi import FastAPI, UploadFile, File
from fastapi.middleware.cors import CORSMiddleware
import tensorflow as tf
import numpy as np
from PIL import Image
import io

app = FastAPI(title="ECG Classifier API")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

MODEL_PATH = "MobileNetV2_best.h5"
model = tf.keras.models.load_model(MODEL_PATH)
IMG_SIZE = 224
THRESHOLD = 0.9


def is_likely_ecg(image: Image.Image) -> bool:
    """Heuristique pour détecter si l'image ressemble à un ECG."""
    img_small = image.resize((150, 150)).convert("RGB")
    pixels = np.array(img_small).astype(int)

    r, g, b = pixels[:, :, 0], pixels[:, :, 1], pixels[:, :, 2]

    # Saturation par pixel (les ECG sont peu colorés)
    saturation = np.maximum(np.maximum(r, g), b) - np.minimum(np.minimum(r, g), b)
    avg_saturation = saturation.mean()

    # % de pixels quasi-blancs (le fond papier d'un ECG domine largement)
    near_white = np.mean((r > 180) & (g > 180) & (b > 180))

    # Contraste global (lignes nettes sur fond clair)
    contrast = pixels.std()

    is_low_saturation = avg_saturation < 40
    has_dominant_white_bg = near_white > 0.45
    has_contrast = contrast > 15

    return is_low_saturation and has_dominant_white_bg and has_contrast


@app.get("/")
def read_root():
    return {"message": "ECG Classifier API is running"}


@app.post("/predict")
async def predict(file: UploadFile = File(...)):
    try:
        contents = await file.read()

        if len(contents) == 0:
            return {"error": "Empty file received. Please upload a valid image."}

        try:
            image = Image.open(io.BytesIO(contents)).convert("RGB")
        except Exception:
            return {"error": "Could not read this file as an image. Please upload a JPG or PNG."}

        if not is_likely_ecg(image):
            return {
                "label": "Not an ECG",
                "confidence": 0,
                "raw_score": 0,
                "recommendation": "This does not appear to be an ECG signal. Please upload a valid ECG image."
            }

        image_resized = image.resize((IMG_SIZE, IMG_SIZE))
        img_array = np.array(image_resized) / 255.0
        img_array = np.expand_dims(img_array, axis=0)

        prediction = model.predict(img_array)[0][0]

        is_normal = prediction > THRESHOLD
        label = "Normal" if is_normal else "Abnormal"
        confidence = float(prediction) if is_normal else float(1 - prediction)

        return {
            "label": label,
            "confidence": round(confidence * 100, 2),
            "raw_score": float(prediction),
            "recommendation": "Please consult a doctor for confirmation" if label == "Abnormal" else "Result appears normal, but please consult a doctor for a professional medical evaluation"
        }

    except Exception as e:
        return {"error": f"An unexpected error occurred: {str(e)}"}

if __name__ == "__main__":
    import uvicorn
    import os
    port = int(os.environ.get("PORT", 8000))
    uvicorn.run(app, host="0.0.0.0", port=port)