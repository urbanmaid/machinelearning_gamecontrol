# Project Overview

This project introduces an AI-powered anti-cheat solution designed to detect hardware spoofing (such as unauthorized keyboard and mouse adapters masking as controllers) in real-time. By analyzing human gameplay behavior—specifically movement and aiming patterns—the system accurately classifies whether the input originates from a **Keyboard & Mouse** or a **Gamepad**.

Built with an optimized lightweight architecture, this system ensures robust anti-cheat monitoring with minimal impact on game performance and player experience.

# Repository Structure

- **Google Colab Notebook (`/notebooks`)**
    - Contains the complete pipeline for data preprocessing, feature extraction, and model training.
    - Trains a lightweight LSTM network optimized to capture temporal gaming context.
    - Exports the final trained model into the highly compatible **ONNX** format.
- **Unity Scripts (`/component_unity`)**
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

# How to test

## Data **extraction**

- For Unity
    - Use component from `/component_unity/InputDataLogger.cs` and apply this component to player object which has input system.
    - Set `log_saving_directory` in inspector for saving log data.
    - Play several times with KB&Mouse and Gamepad.
        - Note: You need to perform the control with the intention of gameplay.
    - Check the file of `documents/{logSavingDirectory}`.

## Training and Generating model

- For Google Colab
    - Upload `/notebooks/GameControllerDistinguisher.ipynb` on your folder.
    - Set `data_folder_path` as directory where you stored data that is using for training.
    - Run and download extracted model (`model.onnx`).
- For local
    - To be written.

## Evaluation

- For Unity
    - Use component from `/component_unity/ControllerDetector.cs` and apply this component to player object which has input system.
    - Set `evaluation_saving_directory` in inspector for saving evaluation data.
    - Play several times with KB&Mouse and Gamepad.
    - Check the file of `documents/{evaluation_saving_directory}`

# Basis project that tested

- **Charming Illusion - Third-person shooter-based squad combat game**

The repository and planning documents of the base project on which this project was conducted are currently being prepared for external release.