"""
Utility functions for HINTS examination data visualization.
Currently only plot_sample is actively used in the analysis notebooks.
"""

import plotly.express as px


def plot_sample(df, label, patient_id, experiment, show_plot=True):
    """Create a line plot of time series data from HINTS examination.

    Args:
        df (DataFrame): Data containing time series measurements
        label (int): Patient label (0=healthy, 1=unhealthy)
        patient_id (int): Unique patient identifier
        experiment (str): Name of the experiment/measurement
        show_plot (bool, optional): Whether to display plot. Defaults to True.

    Returns:
        plotly.graph_objects.Figure: Generated plot figure
    """
    # Set title based on patient label
    if label == 1:
        title = f"Unhealthy Patient: {patient_id}; Experiment: {experiment}"
    elif label == 0:
        title = f"Healthy Patient: {patient_id}; Experiment: {experiment}"
    else:
        title = f"Patient: {patient_id}; Experiment: {experiment}"

    # Remove delta_time if present
    if 'delta_time' in df.columns:
        df.drop(['delta_time'], axis=1, inplace=True)

    # Create line plot
    fig = px.line(df, x=df.time, y=df.columns, title=title)
    
    # Configure axes
    fig.update_yaxes(title='Feature')
    fig.update_xaxes(title='Timestamp', dtick="M1", tickformat="%b\n%Y")
    
    # Set layout
    fig.update_layout(
        template="plotly_white",
        width=15*80,
        height=7*80
    )

    if show_plot:
        fig.show()
    return fig