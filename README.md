# HINTS Examination System for HoloLens 2

## Overview

This repository contains the software components developed for an academic paper on the HINTS (Head Impulse, Nystagmus, Test of Skew) examination using Microsoft HoloLens 2 Mixed Reality headset. The system consists of two main components:

1. **Unity Application for HoloLens 2**: A dedicated application designed to record eye and head tracking data during the HINTS examination protocol, or as an alternative for HINTS exam performed by experts.
2. **Python Processing Pipeline**: Scripts and notebooks for processing, analyzing, and visualizing the raw data collected by the HoloLens 2 application.

This system enables clinicians and researchers to conduct standardized HINTS examinations, while capturing precise eye and head movement data for later analysis.

## System Components

### 1. HoloLens 2 Unity Application

The Unity application (`unity_app_hl2`) is designed to run on Microsoft HoloLens 2 and guides the examiner through the standardized HINTS protocol while recording eye tracking, head movement, and audio data.

#### Key Features

- **Structured Examination Protocol**: Guides the examiner through each step of the HINTS examination with timed audio prompts
- **Real-time Eye Tracking**: Captures detailed eye gaze data using HoloLens 2's eye tracking capabilities
- **Head Movement Tracking**: Records head position and rotation during examination
- **Audio Recording**: Captures audio for clinical notes and observations
- **Data Persistence**: Saves all recorded data in structured format for later analysis

#### Technical Details

- Developed with Unity (optimized for HoloLens 2)
- Utilizes Mixed Reality Toolkit (MRTK) for HoloLens 2 integration
- Implements `ExtendedEyeGazeDataProvider` for enhanced eye tracking capabilities
- Built with C# following component-based architecture principles

### 2. Python Data Processing Pipeline

The Python component (`python_processing_raw_data`) provides a complete pipeline for processing, analyzing, and visualizing the raw data collected by the HoloLens 2 application.

#### Key Features

- **Raw Data Processing**: Converts raw .txt files into structured CSV format
- **Data Organization**: Organizes data by patient and measurement actions
- **Visualization Tools**: Provides comprehensive data visualization with Plotly
- **Analysis Framework**: Supports feasibility analysis and feature extraction
- **Containerized Environment**: Includes Docker configuration for reproducible analysis

#### Technical Details

- Python-based processing pipeline
- Jupyter notebooks for interactive analysis
- Dockerized environment for easy setup and reproducibility

## Setup & Installation

### HoloLens 2 Unity Application

1. **Prerequisites**:
   - Unity 2021.3 LTS or later
   - Mixed Reality Toolkit (MRTK) for Unity
   - Visual Studio 2019 or later with UWP development tools
   - Windows 10 with Windows SDK 18362 or later

2. **Installation**:
   - Clone this repository
   - Open the project in Unity (`unity_app_hl2/SampleEyeTrackingHL2hints`)
   - Configure your development environment for HoloLens 2 using MRTK
      - known issues
         - import the 'Newtonsoft' NuGet package manually if Newtonsoft namespace is missing.
         - 'Extended Eye Gaze Data Provider' inside the 'ExtendedEyeTrackerHLhints' game object can be missing -> simply drag the object into the field stating it's missing.
   - Build the solution for ARM64 architecture
   - Deploy to HoloLens 2 using Visual Studio or the Device Portal

3. **Usage**:
   - Launch the application on HoloLens 2
   - Perform calibration when prompted
   - Follow audio instructions for each examination step
   - Data will be automatically saved to the device for later export

### Python Processing Pipeline

1. **Option 1: Using Docker (Recommended)**:
   ```bash
   # Navigate to the python_processing_raw_data directory
   cd python_processing_raw_data
   
   # Build and start the Docker container
   docker-compose up --build -d
   
   # Access the notebooks via Visual Studio Code or your browser
   # (follow the README.md in python_processing_raw_data for details)
   ```

2. **Option 2: Local Setup**:
   ```bash
   # Navigate to the python_processing_raw_data directory
   cd python_processing_raw_data
   
   # Install dependencies
   pip install --no-cache-dir -r requirements.txt
   
   # Run the notebooks using Jupyter
   jupyter notebook
   ```

3. **Processing Data**:
   - Place raw data files in the `app/RawData/` directory
   - Run `1_PreProcessRawData.ipynb` to convert and organize the data
   - Run `2_plot4feasibilityStudy.ipynb` to generate visualizations and analysis

## Data Structure

### Raw Data (from HoloLens 2)
- `.txt` files containing eye tracking and head movement data
- `.wav` files for audio recordings

### Processed Data
- Structured CSV files organized by patient and examination action
- Generated features and labels for analysis
- Visualization outputs and images

## Usage Examples

### HoloLens 2 Application
The application guides clinicians through the following examination steps:
1. Initial setup and calibration
2. Room examination (scanning the environment)
3. Stationary gaze fixation
4. Nose examination (convergence test)
5. Left and right gaze tests
6. Ceiling and floor gaze tests
7. Head movement test (for vestibulo-ocular reflex)

### Data Analysis
The Python notebooks demonstrate:
1. Data preprocessing and organization
2. Visualization of eye tracking patterns
3. Feature extraction for potential machine learning tasks
4. Quality assessment of collected data

## Research Context

This software was developed as part of academic research investigating the use of mixed reality and eye tracking for vestibular assessment. The HINTS exam is a critical tool for differentiating central from peripheral causes of acute vestibular syndrome.

This implementation allows for:
- Standardized examination protocol
- Objective measurement of eye and head movements
- Data collection for further analysis and potential diagnostic assistance

## Citation

If you use this code or data in your research, please cite our paper:
[Citation information will be added upon publication]

## License

This project is licensed under the terms included in the LICENSE file.

## Acknowledgments

- Microsoft Mixed Reality Team for HoloLens 2 and MRTK
- Contributors to the research and development of this system
- All volunteers who participated in data collection 