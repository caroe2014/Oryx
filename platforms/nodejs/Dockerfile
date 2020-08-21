FROM buildpack-deps:stretch
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        jq \
    && rm -rf /var/lib/apt/lists/*
RUN curl -sL https://git.io/n-install | bash -s -- -ny -
RUN mkdir -p /tmp/scripts
COPY platforms/nodejs/build.sh /tmp/scripts
COPY platforms/nodejs/getYarn.sh /tmp/scripts
COPY images/receiveGpgKeys.sh /tmp/scripts
RUN chmod +x /tmp/scripts/build.sh \
    && chmod +x /tmp/scripts/getYarn.sh \
    && chmod +x /tmp/scripts/receiveGpgKeys.sh \
    && /tmp/scripts/receiveGpgKeys.sh 6A010C5166006599AA17F08146C2130DFD2497F5
