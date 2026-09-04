FROM python:3.11-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy simulator files and protobuf bindings
COPY proto/ proto/
COPY fleet_simulator.py .
COPY wmic_simulator.py .

ENTRYPOINT ["python", "-u", "fleet_simulator.py"]