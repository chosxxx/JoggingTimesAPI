import requests
import json
import unittest
import config

from faker import Faker
fake = Faker()

class Test_test_user_actions(unittest.TestCase):
    test_user = { 'Role': 1 }
    headers = { 'Content-Type': 'application/json' }
    
    def test_user_end_to_end(self):
        def validate_register_user(self):
            url = config.API_CONFIG["url"] + "user/register"
            payload = self.test_user
            resp = requests.put(url, headers=self.headers, data=json.dumps(payload))       
    
            # Asserts
            assert resp.status_code == 200
            resp_body = resp.json()
            assert resp_body['username'] == self.test_user["Username"]
            assert resp_body['role'] == self.test_user["Role"]

        def validate_authenticate_user(self):
            url = config.API_CONFIG["url"] + "user/authenticate"
            payload = self.test_user
            resp = requests.post(url, headers=self.headers, data=json.dumps(payload))

            # Asserts
            assert resp.status_code == 200
            resp_body = resp.json()
            assert resp_body['username'] == self.test_user["Username"]
            self.headers["Authorization"] = "Bearer " + resp_body["token"];

        def validate_get_user(self):
            url = config.API_CONFIG["url"] + "user/" + self.test_user["Username"]
            resp = requests.get(url, headers=self.headers)       
    
            # Asserts
            assert resp.status_code == 200
            resp_body = resp.json()
            assert resp_body['username'] == self.test_user["Username"]
            assert resp_body['role'] == self.test_user["Role"]

        def validate_get_all_user(self):
            url = config.API_CONFIG["url"] + "user/get"
            payload = { 'filter': '', 'rowsPerPage': 10, 'pageNumber': 1 }
            resp = requests.get(url, headers=self.headers, data=json.dumps(payload))       
    
            # Asserts
            assert resp.status_code == 200
            resp_body = resp.json()
            assert not len(resp_body)

        def validate_update_user(self):
            url = config.API_CONFIG["url"] + "user/update"
            payload = { "Username": self.test_user["Username"], "Password": fake.password(12) }
            resp = requests.put(url, headers=self.headers, data=json.dumps(payload))       
    
            # Asserts
            assert resp.status_code == 200
            resp_body = resp.json()
            assert resp_body['username'] == self.test_user["Username"]
            assert resp_body['role'] == self.test_user["Role"]

        def validate_delete_user(self):
            url = config.API_CONFIG["url"] + "user/delete/" + self.test_user["Username"]
            resp = requests.delete(url, headers=self.headers)       

            assert resp.status_code == 200
        
        self.test_user["Username"] = fake.user_name()
        self.test_user["EmailAddress"] = fake.safe_email()
        self.test_user["Password"] = fake.password(length=10)

        validate_register_user(self)
        validate_authenticate_user(self)
        validate_get_user(self)
        validate_get_all_user(self)
        validate_update_user(self)
        validate_delete_user(self)

if __name__ == '__main__':
    unittest.main()
