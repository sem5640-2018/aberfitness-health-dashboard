echo "$ENCRYPTED_DOCKER_PASSWORD" | docker login -u "$ENCRYPTED_DOCKER_USERNAME" --password-stdin
cd health-dashboard
docker build -t sem56402018/health-dashboard:$1 -t sem56402018/health-dashboard:$TRAVIS_COMMIT .
docker push sem56402018/health-dashboard:$TRAVIS_COMMIT
docker push sem56402018/health-dashboard:$1
