# HINTS exam via HoloLens 2 - Data Processing and Analysis

## Introduction

This repository contains scripts and Jupyter notebooks for processing, analyzing, and visualizing raw eye/head-tracking data recorded with the HL2. The data was recorded using our in-house MR-OST-HMD application which simulates the HINTS exam. This project demonstrates a complete data processing pipeline, ensuring reproducibility and transparency in data handling.

There are two main notebooks included:

- **1_PreProcessRawData.ipynb**: Prepares raw data collected from the HL2 device by converting raw .txt files into structured CSV files. The process involves extracting the most recent measurement per patient and organizing data into different classes (e.g., Healthy, Skew, Saccades, NystagmusLeft, etc.).
- **2_plot4feasibilityStudy.ipynb**: Provides visual analysis and feasibility study of the processed data using Plotly. This notebook helps validate data quality and explores potential feature extraction for machine learning tasks.

## Repository Structure

- **app/RawData/**: Contains the raw data files (.txt and .wav) captured by the HL2 application.
*Please note that all subjects are the same healthy volunteers to guarantee data privacy*.
- **app/src/**: Holds the Jupyter notebooks and processing scripts:
  - 1_PreProcessRawData.ipynb
  - 2_plot4feasibilityStudy.ipynb
- **data/**: Created after running the notebook *1_PreProcessRawData*. Stores processed CSV files, organized by patients and measurement actions, as well as the generated features and labels after execution. After running *2_plot4feasibilityStudy*, there will be a folder images containing the images.
- **requirements.txt**: Specifies the Python dependencies with version details.
- **Dockerfile**, **docker-compose.yml** and **.devcontainer.json**: Provide a Dockerized development environment for consistent and hassle-free setup; Alternatively the **requirements.txt** can be used to setup your own virtual environment.

## Requirements

There are two options to set up your environment:

### Option 1: Using Docker

A Docker environment is provided so you can run the notebooks without manually installing dependencies.

1. Clone the repository, go to the cloned directory.
2. Start the Docker container in detached mode:

   ```bash
   docker-compose up --build -d
   ```
3. Open VScode:
4. cntrl + shift + p -> >attach to running container
5. If needed open the */app/* folder inside the container.
6. Run both notebook scripts. 
*Install python/jupyter extensions inside container if it's not already done.*
7. Shut down the docker and clean the system
   ```bash
   docker-compose down
   docker system prune
   ```

### Option 2: Setting Up Locally

If you prefer your own environment, install the dependencies using the provided requirements.txt:

```bash
pip install --no-cache-dir -r requirements.txt
```

Then, start the environment via cntrl + shift + p -> >Python: Select Interpreter


## Running the Notebooks

- **1_PreProcessRawData.ipynb**:
  - Converts raw .txt files in the **RawData/** folder into structured CSV files.
  - Organizes data per patient and measurement action (e.g., raum, still, nase, links, rechts, decke, bodem, blingHeadTest).
  - Generates outputs in the **data/csv_per_patient_per_measurement_action** and **data/hints_features_labels** directories.

- **2_plot4feasibilityStudy.ipynb**:
  - Visualizes the processed data using Plotly.
  - Provides insights into data quality and supports feasibility analysis for further machine learning processing.

## Customization

Users can choose to work within the provided Docker environment or set up their own using the `requirements.txt`. Note that the Docker setup mounts the project into the container (at `/app`), ensuring that the folder structure is consistent.

If you adjust the folder structure or working directories, please update the paths in the notebooks accordingly.

## Additional Information

For more detailed context:

- Refer to the markdown cells in **1_PreProcessRawData.ipynb** for an in-depth explanation of the data preprocessing steps.
- The **2_plot4feasibilityStudy.ipynb** notebook includes commentary and visualizations (after exectution) that illustrate the data analysis process.

## Prerequisites

- Python 3.8+
- Required Python packages:
  - pandas==2.2.2
  - plotly==6.0.1
  - notebook==7.3.3 
  - tqdm==4.67.1
  - matplotlib==3.10.0
  - ipykernel==6.29.5
  - jupyter==1.1.1
  - numpy==2.2.4


## Setup

1. Clone the repository:
```bash
git clone https://...
```

2. Install required Python packages:
```bash
pip install -r requirements.txt
```

3. Run the scripts to generate the Data and Images folders:
```bash
python process_data.py
```

--- 

## Citation / Reference

If you use this code in your research, please cite our paper: tba