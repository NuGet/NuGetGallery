A package for identifying nuget clients from user agents


### Local setup
To setup your local environment, you'll need Python 3.10 or later installed machine-wide, [pipx](https://pipx.pypa.io/stable/), and [Poetry](https://python-poetry.org/) (installed with pipx).

On Windows, [Scoop](https://scoop.sh/) is the easiest way to install Python.

Install pipx as per the [instructions](https://pipx.pypa.io/latest/installation/). On Windows, Scoop is the easiest way to install Python and pipx. Once you have pipx, you can use it to install poetry.


### Activating your python environment
In your CLI, go to the `python/StatsLogParser` directory and then run `poetry install` to create the virtual environment and install dependencies into it.

### Dev and Test
VS Code is recommended -- open the `python/StatsLogParser` folder and ensure you have the Python extensions installed. Poetry extensions are recommended too for environment activation. Testing is with pytest.
To test on the CLI, you can run `poetry run pytest tests/` and to get code coverage, `poetry run pytest tests/ --cov loginterpretation --cov-report html`

### Dependencies
Dependencies are in the `pyproject.toml` file. If you add/update dependencies, run `poetry export -f requirements.txt --output requirements.txt` to update the `requirements.txt` as both the that file and the `whl` will be needed for Spark.

### Building the wheel
Run poetry build from the CLI and it'll build the wheel package in `dist`.
