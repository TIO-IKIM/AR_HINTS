"""
Data processing module for HINTS examination analysis.
Provides functionality for loading, preprocessing, and analyzing eye tracking data
from different examination sessions and perspectives.
"""

import os
import pandas as pd
from utils import *


def data_path(data_session="still"):
    """Get paths for features and labels from current data collection.

    Args:
        data_session (str): Session type ('raum', 'nase', 'still', 'blingHeadTest', etc.)

    Returns:
        tuple: Paths to features and labels directories
    """
    if data_session:
        features_dir = f'../data/hints_features_labels/{data_session}/features/'
        labels_dir = f'../data/hints_features_labels/{data_session}/labels/'
        return features_dir, labels_dir


def generate_data(features_dir, labels_dir, sort=False, perspective="camera",
                 raw=False, time_range=[]):
    """Generate processed datasets from raw CSV files.

    Args:
        features_dir (str): Directory containing feature CSV files
        labels_dir (str): Directory containing label files
        sort (bool): Whether to sort sequences by person ID
        perspective (str): Data perspective ('camera' or 'world')
        raw (bool): Whether to use raw data
        time_range (list): Time window for data selection [start, end]

    Returns:
        tuple: (sequences, features, labels) processed data
    """
    sequences = []
    features = []
    labels = []

    for filename in os.listdir(features_dir):
        if filename.endswith('.csv'):
            # Load and preprocess data
            df = pd.read_csv(os.path.join(features_dir, filename))

            if raw:
                df['time'] = df['delta_time'].cumsum()
                df = df[(df['time'] >= time_range[0]) &
                       (df['time'] <= time_range[1])].dropna()
            else:
                if perspective == "world":
                    df = df.iloc[:, 1:20].dropna()
                    df['time'] = df['delta_time'].cumsum()
                    df = df[(df['time'] >= time_range[0]) &
                           (df['time'] <= time_range[1])].dropna()
                elif perspective == "camera":
                    dt = df.iloc[:, 1:2]
                    rest = df.iloc[:, 20:-10]
                    df = pd.concat([dt, rest], axis=1).dropna()
                    df['time'] = df['delta_time'].cumsum()
                    df = df[(df['time'] >= time_range[0]) &
                           (df['time'] <= time_range[1])].dropna()

            # Load corresponding label
            label_filename = os.path.join(labels_dir, filename)
            with open(label_filename) as f:
                label = int(f.readline().strip())

            features.append(df)
            labels.append(label)
            sequences.append((df, label))

    if sort:
        sequences.sort(key=lambda x: x[1])
    return sequences, features, labels


def colsToDrop(df, dropstring):
    """Drop columns containing specific string pattern.

    Args:
        df (DataFrame): Input DataFrame
        dropstring (str): String pattern to match for dropping columns

    Returns:
        DataFrame: DataFrame with matched columns removed
    """
    drop_it = []
    for col in df:
        if dropstring in col:
            drop_it.append(col)
    df = df.drop(columns=drop_it)
    return df
