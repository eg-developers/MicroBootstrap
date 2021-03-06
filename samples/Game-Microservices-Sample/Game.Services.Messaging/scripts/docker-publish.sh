#!/bin/bash
DOCKER_TAG=''

case "$TRAVIS_BRANCH" in
  "master")
    DOCKER_TAG=latest
    ;;
  "develop")
    DOCKER_TAG=dev
    ;;    
esac
docker login -u $DOCKER_USERNAME -p $DOCKER_PASSWORD
docker build  -t game.services.messaging:$DOCKER_TAG -f ./Dockerfile .
docker tag game.services.messaging:$DOCKER_TAG $DOCKER_USERNAME/game.services.messaging:$DOCKER_TAG
docker push $DOCKER_USERNAME/game.services.messaging:$DOCKER_TAG