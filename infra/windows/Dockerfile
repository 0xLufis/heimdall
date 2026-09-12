FROM dockurr/windows:latest

# Default to Windows 10 LTSC (evaluation image, lean footprint)
ENV VERSION="10l"
ENV RAM_SIZE="3G"
ENV CPU_CORES="2"
ENV DISK_SIZE="20G"

# Unattended credentials
ENV USERNAME="Docker"
ENV PASSWORD="Password123!"

# Copy OEM unattended post-install provisioning scripts
COPY oem/ /oem/

# Expose Web VNC (8006), RDP (3389), WinRM HTTP (5985), Agent Configurator (5998), OpenSSH (22)
EXPOSE 8006 3389 5985 5998 22
