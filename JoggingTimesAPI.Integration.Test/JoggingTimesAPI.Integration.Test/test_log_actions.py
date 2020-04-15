import requests
import json
import unittest
import config

from faker import Faker
fake = Faker()

class Test_test_log_actions(unittest.TestCase):
    test_user = { 'Role': 1 }
    headers = { 'Content-Type': 'application/json' }
    logid = 0

    def test_log_end_to_end(self):
        def register_and_authenticate_user(self):
            # Register
            url = config.API_CONFIG["url"] + "user/register"
            payload = self.test_user
            resp = requests.put(url, headers=self.headers, data=json.dumps(payload))  
            assert resp.status_code == 200

            # Authenticate
            url = config.API_CONFIG["url"] + "user/authenticate"
            resp = requests.post(url, headers=self.headers, data=json.dumps(payload))
            assert resp.status_code == 200
            resp_body = resp.json()
            assert resp_body['username'] == self.test_user["Username"]
            self.headers["Authorization"] = "Bearer " + resp_body["token"];

        def validate_start_log(self):
            url = config.API_CONFIG["url"] + "joggingtimelog/start"
            payload = { 
                'latitude': "{0:f}".format(fake.latitude()), 
                'longitude': "{0:f}".format(fake.longitude()) 
            }
            resp = requests.put(url, headers=self.headers, data=json.dumps(payload))

            # Asserts
            assert resp.status_code == 200
            resp_body = resp.json()
            self.logid = resp_body["joggingTimeLogId"]
            
        def validate_update_log(self):
            url = config.API_CONFIG["url"] + "joggingtimelog/update"
            payload = { 
                'JoggingTimeLogId': self.logid, 
                'DistanceMetres': "{0:f}".format(fake.pydecimal(min_value=1, max_value=100)) 
            }
            resp = requests.put(url, headers=self.headers, data=json.dumps(payload))

            # Asserts
            assert resp.status_code == 200
            resp_body = resp.json()
            assert resp_body["joggingTimeLogId"] == self.logid

        def validate_stop_log(self):
            url = config.API_CONFIG["url"] + "joggingtimelog/stop"
            payload = { 
                'JoggingTimeLogId': self.logid, 
                'DistanceMetres': "{0:f}".format(fake.pydecimal(min_value=101, max_value=200)) 
            }
            resp = requests.put(url, headers=self.headers, data=json.dumps(payload)) 

            # Asserts
            assert resp.status_code == 200
            resp_body = resp.json()
            assert resp_body["joggingTimeLogId"] == self.logid

        def validate_get_all_log(self):
            url = config.API_CONFIG["url"] + "joggingtimelog/get"
            payload = { 'filter': '', 'rowsPerPage': 10, 'pageNumber': 1 }
            resp = requests.get(url, headers=self.headers, data=json.dumps(payload))       
    
            # Asserts
            assert resp.status_code == 200
            resp_body = resp.json()
            assert len(resp_body)

        def validate_delete_log(self):
            url = config.API_CONFIG["url"] + "joggingtimelog/delete/" + "{0:d}".format(self.logid)
            resp = requests.delete(url, headers=self.headers) 

            # Asserts
            assert resp.status_code == 200
            resp_body = resp.json()
            assert resp_body["joggingTimeLogId"] == self.logid

        def delete_user(self):
            url = config.API_CONFIG["url"] + "user/delete/" + self.test_user["Username"]
            resp = requests.delete(url, headers=self.headers)
            assert resp.status_code == 200

        self.test_user["Username"] = fake.user_name()
        self.test_user["EmailAddress"] = fake.safe_email()
        self.test_user["Password"] = fake.password(length=10)

        register_and_authenticate_user(self)
        validate_start_log(self)
        validate_update_log(self)
        validate_stop_log(self)
        validate_get_all_log(self)
        validate_delete_log(self)
        delete_user(self)

if __name__ == '__main__':
    unittest.main()