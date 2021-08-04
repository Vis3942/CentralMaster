#!/bin/bash

UPDATE_FILE="/tmp/$1"
CERT_FILE="$2"
echo "CERT FILE = $CERT_FILE"
if test -f "$UPDATE_FILE"; then
	tar -C /tmp -xzvf "$UPDATE_FILE"
	DIGEST_FILE=${UPDATE_FILE/_fw.tar.gz/.digest}
	FW_FILE=${UPDATE_FILE/_fw.tar.gz/.tar.gz}
	echo "$DIGEST_FILE"
	echo "$FW_FILE"
	openssl dgst -verify "$CERT_FILE" -keyform PEM -sha256 -signature "$DIGEST_FILE" -binary "$FW_FILE"
	VERIFY_TRUE="$?"
	if [ "$VERIFY_TRUE" == "1" ]; then
		rm "$FW_FILE"
		rm "$DIGEST_FILE"
		rm "$UPDATE_FILE"
	else
		rm "$DIGEST_FILE"
		rm "$UPDATE_FILE"
	fi
fi

