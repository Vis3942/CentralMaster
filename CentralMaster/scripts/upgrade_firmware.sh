#!/bin/bash

APP_PATH="$1"
TAR_FILE_PATH="/tmp/$2"
APP_NAME="$3"
#FW_FILE_NAME="$(basename $UPDATE_FILE_PATH)"
echo "$FW_FILE_NAME"
#APP_PATH="$(dirname $UPDATE_FILE_PATH)"
echo "$APP_PATH"
NEW_DIR=`date +"%Y%m%d%H%M%S"`
NEW_PATH="$APP_PATH/$NEW_DIR"
echo $NEW_PATH
mkdir -p $NEW_PATH
tar -C $NEW_PATH -xzf $TAR_FILE_PATH
echo "changing permission for "
#echo "$NEW_PATH/$FW_FILE_NAME"
echo "$NEW_PATH/$APP_NAME"
chmod 755 "$NEW_PATH/$APP_NAME"
rm $TAR_FILE_PATH
cd $APP_PATH
ln -sf $NEW_DIR current-tmp
if [ "$APP_NAME" == "StatusServices" ]; then
	systemctl stop statusservice.service
elif [ "$APP_NAME" == "CentralMaster" ]; then
	systemctl stop centralmaster.service
	chmod 755 "$NEW_PATH/scripts/check_firmware.sh"
	chmod 755 "$NEW_PATH/scripts/upgrade_firmware.sh"
elif [ "$APP_NAME" == "DataServices" ]; then
	systemctl stop dataservices.service
else
	echo "Unknown $APP_NAME"
fi
mv -fT current-tmp current
if [ "$APP_NAME" == "StatusServices" ]; then
	systemctl start statusservice.service
elif [ "$APP_NAME" == "CentralMaster" ]; then 
	systemctl start centralmaster.service
elif [ "$APP_NAME" == "DataServices" ]; then
	systemctl start dataservices.service
else
	echo "Unknown $APP_NAME"
fi
#clear up if there are more than 3 directories
NUM_DIRS="`ls -lt $APP_PATH | grep "^d" | wc -l`"
echo $NUM_DIRS
if [ $NUM_DIRS -gt 3 ]; then
	NUM_DIRS=$((NUM_DIRS-3))
	eval "ls -lt $APP_PATH | grep "^d" | tail -$NUM_DIRS | xargs rm -rf"
fi

	
