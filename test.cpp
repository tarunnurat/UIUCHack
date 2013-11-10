#include <iostream>
#include <fstream>
#include <sstream>
#include <string>
#include <cstdlib>
#include <vector>

using namespace std;

double total_x = 0;;
double total_y = 0;
double total_z = 0;
double totalVertices = 0;
vector<double> all_x;
vector<double> all_y;
vector<double> all_z;

const void parseLine(const string & line) {
	++totalVertices;
	int counter = 0;
	string x = "";
	string y = "";
	string z = "";
	for (int i = 1; i < line.length(); ++i) {
		if (line[i] == ' ') {
			++counter;
			continue;
		}
		if (counter == 1) {
			x += line[i];
		}
		else if (counter == 2) {
			y += line[i];
		}
		else{
			z += line[i];
		}
	}
	double double_x = ::atof(x.c_str());
	double double_y = ::atof(y.c_str());
	double double_z = ::atof(z.c_str());
	all_x.push_back(double_x);
	all_y.push_back(double_y);
	all_z.push_back(double_z);
	total_x += double_x;
	total_y += double_y;
	total_z += double_z;
}

string getStringFromDouble(double num) {
	ostringstream sstream;
	sstream << num;
	string varAsString = sstream.str();
	return varAsString;
}

void shiftVertices(double average_x, double average_y, double average_z) {
	for (int i = 0; i < all_x.size(); i++) {
		all_x[i] -= average_x;
		all_y[i] -= average_y;
		all_z[i] -= average_z;
		string string_x = getStringFromDouble(all_x[i]);
		string string_y = getStringFromDouble(all_y[i]);
		string string_z = getStringFromDouble(all_z[i]);
		string verticeLine = "v " + string_x + " " + string_y + " " + string_z + "\n";
		//cout << verticeLine;
	}
}

void parseFaceLine(const string & line) {
	int counter = 0;
	string f1 = "";
	string f2 = "";
	string f3 = "";
	bool shouldParse = false;
	for (int i = 1; i < line.length(); ++i) {
		if (line[i] == ' ') {
			++counter;
			shouldParse = true;
			continue;
		}
		if (line[i] == '/') {
			shouldParse = false;
		}
		if (counter == 1 && shouldParse) {
			f1 += line[i];
		}
		else if (counter == 2 && shouldParse) {
			f2 += line[i];
		}
		else if(counter == 3 && shouldParse){
			f3 += line[i];
		}
	}
	double double_f1 = ::atof(f1.c_str());
	double double_f2 = ::atof(f2.c_str());
	double double_f3 = ::atof(f3.c_str());

	string string_x = getStringFromDouble(double_f1);
	string string_y = getStringFromDouble(double_f2);
	string string_z = getStringFromDouble(double_f3);
	//cout << "f "<< string_x + " " << string_y << " " << string_z << "\n";
}

int main() {
	string line;
	ifstream myfile ("Pratik.obj");
	if (myfile.is_open()) {
		while ( getline (myfile,line) )
		{
			if (line[0] == 'v' && line[1] != 'n') {
				parseLine(line);
			}
			else if (line[0] == 'f') {
				parseFaceLine(line);
			}
			else if (line[0] == 'v' && line[1] == 'n') {
				// do nothing.
			}
			else {
				//cout << line + "\n";
			}
		}
		myfile.close();
	}
	double average_x = total_x / totalVertices;
	double average_y = total_y / totalVertices;
	double average_z = total_z / totalVertices;
	cout << average_x << " " << average_y << " " << average_z << "\n";
	shiftVertices(average_x, average_y, average_z);
	return 0;
}
