import yaml

with open("LocationServices.yaml", "r") as stream:
    try:
        data = yaml.safe_load(stream)
        for one_value in data['paths'].keys():
            print (one_value)
    except yaml.YAMLError as exc:
        print(exc)