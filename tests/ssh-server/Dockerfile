FROM alpine:3.22
RUN apk add --no-cache openssh-server bash \
 && ssh-keygen -A \
 && adduser -D -s /bin/bash tester \
 && echo 'tester:correct horse battery staple' | chpasswd \
 && mkdir -p /home/tester/.ssh && chmod 700 /home/tester/.ssh && chown tester:tester /home/tester/.ssh
COPY sshd_config /etc/ssh/sshd_config
EXPOSE 22
CMD ["/usr/sbin/sshd", "-D", "-e"]
