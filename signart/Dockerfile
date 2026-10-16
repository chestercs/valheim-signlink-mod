# Sign-art desk: glyph extraction from the game fonts, TMP layout model, renderer, simulator build.
FROM python:3.12-slim
RUN apt-get update && apt-get install -y --no-install-recommends libegl1 libgl1 libfontconfig1 && rm -rf /var/lib/apt/lists/*
RUN pip install --no-cache-dir "UnityPy==1.22.*" lz4 brotli numpy fonttools skia-python pytest
WORKDIR /work
