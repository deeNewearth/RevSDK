This image is a custo jinga2-cli image with additional filters
used to create k8 yml files.

contains script to pull templates from github

USAGE:

* To create YML files from local files
docker run --rm -v %cd%/templates:/templates labizbille/makek8 /scripts/fromexisting


* To create YML files from GitHub
docker run --rm -e SVN_URL=https://github.com/REPONAME/TEMPLATES_PATH  labizbille/makek8 /scripts/fromsvn


* to test new filters
docker run --rm  -it --entrypoint sh     -v %cd%/templates:/templates  cloudconnect.scanrev.com:5000/jinja2docker:1.0.2

 