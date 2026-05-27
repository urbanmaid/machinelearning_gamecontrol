# Project Overview

This project introduces an AI-powered anti-cheat solution designed to detect hardware spoofing (such as unauthorized keyboard and mouse adapters masking as controllers) in real-time. By analyzing human gameplay behavior—specifically movement and aiming patterns—the system accurately classifies whether the input originates from a **Keyboard & Mouse** or a **Gamepad**.

Built with an optimized lightweight architecture, this system ensures robust anti-cheat monitoring with minimal impact on game performance and player experience.

# Repository Structure

- **Google Colab Notebook (`/notebooks`)**
    - Contains the complete pipeline for data preprocessing, feature extraction, and model training.
    - Trains a lightweight LSTM network optimized to capture temporal gaming context.
    - Exports the final trained model into the highly compatible **ONNX** format.
- **Unity Scripts (`/unity-scripts`)**
    - Features a ready-to-use controller detection component attachable to the player object.
    - Utilizes **Unity Sentis** to run real-time inference on runtime input data (`MoveDelta` and `AimDelta`).
    - Logs structured inference results (`.json`) detailing operational timestamps and controller probabilities.
- **Example of Data for training (`/data-train`)**
    - Shows the example of data that is used for training.
    - Consists with controller scheme, time, 2D vector of Move, 2D vector of Look.
- **Example of generated models (`/models`)**
    - Shows the example of model created from this project.

# Key AI Features & Methodology

The model analyzes 60-frame sequences (approx. 0.5 to 1 second of gameplay) using four distinct behavioral features:

| **Feature** | **Target Data** | **Description** |
| --- | --- | --- |
| **Move Digital Flag** | Movement | Checks if movement is binary ($0, 1, -1$) or analog (continuous decimals). |
| **Aim Variance** | Aiming / Looking | Captures the sudden, high-variance direction shifts typical of a mouse. |
| **Max Magnitude** | Aiming / Looking | Exploits software-capped pad rotation speeds vs. unrestricted mouse speed. |
| **Zero Crossing Rate** | Aiming / Looking | Measures the frequency of stopping and starting actions. |

> **Optimization Note:** The architecture incorporates Dropout and L2 Regularization to mitigate overfitting, achieving an outstanding validation accuracy close to 1.0 with a file size of just 128KB.

# Basis project that tested